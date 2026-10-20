using UnityEngine;

namespace Bogatyri.Prototype
{
    // Sidor Zapechny as a grey block: light combo, heavy blow, stomp on the downed, grab and throw, pick up items.
    public class Player : Fighter
    {
        const float WalkSpeed = 72f;
        const float DepthSpeed = 46f;
        const float ComboWindow = 0.4f;

        public int comboHits;
        public float lastLandedTime = -10f;
        public Fighter lastTarget;

        readonly AttackDef[] jabs =
        {
            new AttackDef { name = "Тычок", windup = 0.06f, active = 0.06f, recovery = 0.14f, reachMin = 2, reachMax = 26, damage = 5, force = 40, stun = 0.3f, hitstop = 0.035f, armLength = 14 },
            new AttackDef { name = "Второй тычок", windup = 0.06f, active = 0.06f, recovery = 0.16f, reachMin = 2, reachMax = 26, damage = 6, force = 40, stun = 0.3f, hitstop = 0.04f, armLength = 15 },
            new AttackDef { name = "Добивающий", windup = 0.12f, active = 0.08f, recovery = 0.3f, reachMin = 2, reachMax = 30, damage = 10, force = 150, knockdown = true, hitstop = 0.09f, armLength = 19 },
        };
        readonly AttackDef heavy = new AttackDef { name = "Тяжёлый", windup = 0.3f, active = 0.1f, recovery = 0.35f, reachMin = 2, reachMax = 34, damage = 14, force = 175, knockdown = true, hitstop = 0.1f, armLength = 22 };
        readonly AttackDef stomp = new AttackDef { name = "По лежачему", windup = 0.18f, active = 0.06f, recovery = 0.3f, reachMin = -6, reachMax = 28, depth = 11, damage = 7, hitsDowned = true, hitstop = 0.05f, armLength = 10 };
        readonly AttackDef knee = new AttackDef { name = "Коленом", windup = 0.08f, active = 0.05f, recovery = 0.18f, reachMin = 0, reachMax = 24, damage = 6, stun = 0f, hitstop = 0.05f, armLength = 8 };
        readonly AttackDef poleSwing = new AttackDef { name = "Оглоблей", windup = 0.16f, active = 0.1f, recovery = 0.26f, reachMin = 2, reachMax = 48, depth = 11, damage = 15, force = 100, stun = 0.4f, hitstop = 0.07f, armLength = 36 };

        int comboIndex;
        float comboWindowUntil;
        bool bufferLight, bufferHeavy;

        Fighter grabbed;
        float grabTimer;
        int knees;

        int poleUses;
        SpriteRenderer poleRenderer;
        Prop carried;

        public bool HasPole => poleUses > 0;
        public bool IsCarrying => carried != null;

        public static Player Create(Transform parent, Vector3 pos)
        {
            var go = new GameObject("Сидор");
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            var p = go.AddComponent<Player>();
            p.team = Team.Heroes;
            p.displayName = "СИДОР";
            p.maxHp = 120f;
            p.BuildBody(new Vector2(20, 38), Art.Hex("#8f969c"), Art.Hex("#f1c9a0"), Art.Hex("#6a7076"));
            return p;
        }

        protected override bool DestroyOnDeath => false;

        protected override Vector3 ClampX(Vector3 p) => Stage.I != null ? Stage.I.ClampToScreen(p) : p;

        protected override void Think(float dt)
        {
            if (grabbed != null) HoldGrabbed(dt);

            if (state == FState.Attacking)
            {
                if (GameInput.Light) bufferLight = true;
                if (GameInput.Heavy) bufferHeavy = true;
                TickAttack(dt);
                return;
            }

            bool light = GameInput.Light || bufferLight;
            bool heavyPressed = GameInput.Heavy || bufferHeavy;
            bool grab = GameInput.Grab;
            bufferLight = bufferHeavy = false;

            if (grabbed != null)
            {
                if (light)
                {
                    StartAttack(knee);
                }
                else if (heavyPressed || grab || grabTimer <= 0f)
                {
                    if (grabTimer <= 0f) ReleaseGrab();
                    else ThrowGrabbed();
                }
                return;
            }

            var move = GameInput.Move();
            float speedMul = IsCarrying ? 0.7f : 1f;
            Face(move.x);
            Move(new Vector2(move.x * WalkSpeed, move.y * DepthSpeed) * speedMul * dt);

            if (IsCarrying)
            {
                if (light || heavyPressed || grab) ThrowCarried();
                return;
            }

            if (grab)
            {
                if (!TryPickUp()) TryGrab();
                return;
            }

            if (light)
            {
                if (HasPole)
                {
                    StartAttack(poleSwing);
                }
                else
                {
                    int i = Time.time <= comboWindowUntil ? comboIndex : 0;
                    StartAttack(jabs[i]);
                }
            }
            else if (heavyPressed)
            {
                bool downedAhead = Combat.FindInFront(this, -6, 28, 11, t => t.state == FState.Down) != null;
                StartAttack(downedAhead ? stomp : heavy);
            }
        }

        protected override void OnAttackFinished(AttackDef a)
        {
            int i = System.Array.IndexOf(jabs, a);
            if (i >= 0 && i < jabs.Length - 1)
            {
                comboIndex = i + 1;
                comboWindowUntil = Time.time + ComboWindow;
            }
            else
            {
                comboIndex = 0;
            }

            if (a == knee && grabbed != null)
            {
                knees++;
                if (knees >= 3) ThrowGrabbed();
            }
        }

        protected override void OnAttackLanded(AttackDef a, int hits)
        {
            if (hits <= 0) return;
            if (Time.time - lastLandedTime > 1.5f) comboHits = 0;
            comboHits += hits;
            lastLandedTime = Time.time;
            lastTarget = Combat.FindInFront(this, a.reachMin - 4, a.reachMax + 20, a.depth + 4, t => true) ?? lastTarget;

            if (a == poleSwing && --poleUses <= 0)
            {
                Fx.Debris(transform.position + new Vector3(facing * 30f, 24f), Art.Hex("#8e5f35"), 6);
                SetPole(false);
            }
        }

        // ---------- grab and throw ----------

        void TryGrab()
        {
            var target = Combat.FindInFront(this, 2, 22, 8,
                t => t.height <= 0f && (t.state == FState.Free || t.state == FState.Hurt || t.state == FState.Attacking));
            if (target == null) return;
            grabbed = target;
            grabbed.SetGrabbed(this);
            grabTimer = 1.8f;
            knees = 0;
        }

        void HoldGrabbed(float dt)
        {
            if (grabbed == null || grabbed.state != FState.Grabbed)
            {
                grabbed = null;
                return;
            }
            grabTimer -= dt;
            grabbed.transform.position = transform.position + new Vector3(facing * 15f, 0f, 0f);
        }

        void ThrowGrabbed()
        {
            if (grabbed == null) return;
            var g = grabbed;
            grabbed = null;
            g.ThrowAsProjectile(facing, team);
            Feel.Shake(1.5f, 0.08f);
        }

        void ReleaseGrab()
        {
            if (grabbed != null) grabbed.Release();
            grabbed = null;
        }

        // ---------- items ----------

        bool TryPickUp()
        {
            var item = Pickup.Nearest(transform.position, 18f, 10f);
            if (item == null) return false;
            if (item.kind == Pickup.Kind.Pole)
            {
                SetPole(true);
            }
            else
            {
                carried = Prop.CreateBarrel(Stage.I.transform, transform.position);
                carried.Carry(this);
            }
            Destroy(item.gameObject);
            return true;
        }

        void ThrowCarried()
        {
            if (carried == null) return;
            carried.Throw(facing, team);
            carried = null;
        }

        void SetPole(bool on)
        {
            poleUses = on ? 8 : 0;
            if (on && poleRenderer == null)
            {
                poleRenderer = Art.Box(visual, "Оглобля", new Vector2(14, 22), new Vector2(34, 3), Art.Hex("#8e5f35"));
                Track(poleRenderer, 4);
            }
            if (poleRenderer != null) poleRenderer.gameObject.SetActive(on);
        }

        protected override void Interrupt()
        {
            base.Interrupt();
            ReleaseGrab();
            if (carried != null)
            {
                carried.Break();
                carried = null;
            }
        }

        protected override void LateUpdate()
        {
            base.LateUpdate();
            if (carried != null) carried.Carry(this);
            if (poleRenderer != null && poleRenderer.gameObject.activeSelf)
            {
                // The pole follows the arm: raised on windup, swept forward on the hit.
                bool swinging = state == FState.Attacking && current == poleSwing;
                bool windup = swinging && attackT < poleSwing.windup;
                poleRenderer.transform.localPosition = windup ? new Vector3(-4f, 34f, 0f) : swinging ? new Vector3(26f, 22f, 0f) : new Vector3(10f, 22f, 0f);
                poleRenderer.transform.localRotation = Quaternion.Euler(0f, 0f, windup ? 70f : swinging ? 0f : 35f);
            }
        }
    }
}
