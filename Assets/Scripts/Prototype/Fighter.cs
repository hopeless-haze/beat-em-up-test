using System.Collections.Generic;
using UnityEngine;

namespace Bogatyri.Prototype
{
    public enum FState { Free, Attacking, Hurt, Airborne, Down, Grabbed, Dead }

    // Shared body, health and hit reactions for heroes and enemies.
    // transform.position is the point on the ground; height lifts the visual when knocked into the air.
    public abstract class Fighter : MonoBehaviour
    {
        public static readonly List<Fighter> All = new List<Fighter>();

        const float Gravity = 420f;

        public Team team;
        public string displayName;
        public float maxHp = 100f;
        public float hp;
        public int facing = 1;
        public FState state = FState.Free;
        public float height;
        public float lastHurtTime = -10f;

        protected float stateTimer;
        protected float heightVel;
        protected Vector2 vel;
        protected float invulnerableUntil;

        // Thrown robbers become projectiles until they land.
        protected bool flyingProjectile;
        protected Team projectileTeam;
        readonly HashSet<Fighter> projectileHits = new HashSet<Fighter>();

        protected AttackDef current;
        protected float attackT;
        bool attackHitDone;

        protected Transform visual;
        protected SpriteRenderer body, head, arm;
        Color bodyColor, headColor, armColor;
        float flashTimer;
        readonly List<SpriteRenderer> parts = new List<SpriteRenderer>();
        readonly List<int> partOffsets = new List<int>();
        SpriteRenderer shadow;
        Vector2 bodySize;

        public bool IsAlive => state != FState.Dead;
        public float VelocityX => vel.x;
        protected virtual bool DestroyOnDeath => true;

        protected virtual void OnEnable() => All.Add(this);
        protected virtual void OnDisable() => All.Remove(this);

        protected void BuildBody(Vector2 size, Color bodyCol, Color headCol, Color armCol)
        {
            bodySize = size;
            shadow = Art.Box(transform, "Shadow", new Vector2(0, -2), new Vector2(size.x + 8, 4), new Color(0.08f, 0.12f, 0.08f, 0.3f));
            Track(shadow, -1);
            visual = new GameObject("Visual").transform;
            visual.SetParent(transform, false);
            body = Art.Box(visual, "Body", Vector2.zero, size, bodyCol);
            head = Art.Box(visual, "Head", new Vector2(1, size.y), new Vector2(10, 10), headCol);
            arm = Art.Box(visual, "Arm", new Vector2(size.x * 0.3f, size.y * 0.62f), new Vector2(8, 4), armCol);
            // A dark eye shows which way the figure faces.
            var eye = Art.Box(head.transform, "Eye", new Vector2(0.3f, 0.5f), new Vector2(0.15f, 0.2f), new Color(0.1f, 0.1f, 0.12f));
            Track(body, 0);
            Track(head, 1);
            Track(eye, 2);
            Track(arm, 3);
            bodyColor = bodyCol;
            headColor = headCol;
            armColor = armCol;
            hp = maxHp;
            ApplyFacing();
        }

        protected void Track(SpriteRenderer r, int offset)
        {
            parts.Add(r);
            partOffsets.Add(offset);
        }

        protected void ApplyFacing()
        {
            if (visual != null) visual.localScale = new Vector3(facing, 1f, 1f);
        }

        protected void Face(float dx)
        {
            if (Mathf.Abs(dx) < 0.01f) return;
            facing = dx > 0 ? 1 : -1;
            ApplyFacing();
        }

        // ---------- taking hits ----------

        public virtual bool ReceiveHit(Hit h)
        {
            if (state == FState.Dead || state == FState.Airborne) return false;
            if (Time.time < invulnerableUntil) return false;
            if (state == FState.Down && !h.hitsDowned) return false;

            hp -= h.damage;
            lastHurtTime = Time.time;
            flashTimer = 0.08f;

            int dir = h.dir != 0 ? h.dir
                : h.from != null ? (transform.position.x >= h.from.transform.position.x ? 1 : -1)
                : -facing;

            if (state == FState.Down)
            {
                stateTimer = Mathf.Max(stateTimer, 0.4f);
                if (hp <= 0f) Die();
                return true;
            }

            if (state == FState.Grabbed)
            {
                if (hp <= 0f) Launch(dir * 120f, 100f);
                return true;
            }

            facing = -dir;
            ApplyFacing();
            Interrupt();

            if (hp <= 0f || h.knockdown)
            {
                Launch(dir * h.force, 110f);
            }
            else
            {
                state = FState.Hurt;
                stateTimer = h.stun;
                vel = new Vector2(dir * h.force * 0.5f, 0f);
            }
            return true;
        }

        public void Launch(float vx, float vy)
        {
            Interrupt();
            state = FState.Airborne;
            vel = new Vector2(vx, 0f);
            heightVel = vy;
            height = Mathf.Max(height, 1f);
        }

        public void SetGrabbed(Fighter by)
        {
            Interrupt();
            state = FState.Grabbed;
            facing = -by.facing;
            ApplyFacing();
        }

        public void Release()
        {
            if (state != FState.Grabbed) return;
            state = FState.Hurt;
            stateTimer = 0.25f;
        }

        public void ThrowAsProjectile(int dir, Team thrower)
        {
            projectileHits.Clear();
            flyingProjectile = true;
            projectileTeam = thrower;
            state = FState.Grabbed; // so Launch below does not count as being hit
            facing = -dir;
            ApplyFacing();
            Launch(dir * 190f, 120f);
            height = 20f;
        }

        void Die()
        {
            state = FState.Dead;
            stateTimer = 1.2f;
            Interrupt();
        }

        // Cancels whatever the fighter was doing: attacks, grabs, carried items.
        protected virtual void Interrupt()
        {
            current = null;
        }

        // ---------- attacking ----------

        protected void StartAttack(AttackDef a)
        {
            current = a;
            attackT = 0f;
            attackHitDone = false;
            state = FState.Attacking;
        }

        protected void TickAttack(float dt)
        {
            if (current == null)
            {
                state = FState.Free;
                return;
            }
            attackT += dt;
            if (!attackHitDone && attackT >= current.windup)
            {
                attackHitDone = true;
                OnAttackLanded(current, Combat.HitArea(this, current, ExtraReach(current), DamageMul(current)));
            }
            if (attackT >= current.Total)
            {
                var finished = current;
                current = null;
                state = FState.Free;
                OnAttackFinished(finished);
            }
        }

        protected virtual float ExtraReach(AttackDef a) => 0f;
        protected virtual float DamageMul(AttackDef a) => 1f;
        protected virtual void OnAttackLanded(AttackDef a, int hits) { }
        protected virtual void OnAttackFinished(AttackDef a) { }

        // ---------- movement and update ----------

        public void Move(Vector2 delta)
        {
            var p = transform.position + (Vector3)delta;
            p.y = Mathf.Clamp(p.y, Stage.MinY, Stage.MaxY);
            transform.position = ClampX(p);
        }

        protected virtual Vector3 ClampX(Vector3 p) => p;

        protected abstract void Think(float dt);

        protected virtual void Update()
        {
            float dt = Time.deltaTime;
            switch (state)
            {
                case FState.Airborne:
                    heightVel -= Gravity * dt;
                    height += heightVel * dt;
                    Move(vel * dt);
                    if (flyingProjectile)
                    {
                        Combat.FlyingHits(transform.position, height, vel.x, projectileTeam, projectileHits, this, 12f);
                    }
                    if (height <= 0f) Land();
                    break;

                case FState.Down:
                    stateTimer -= dt;
                    if (stateTimer <= 0f)
                    {
                        state = FState.Free;
                        invulnerableUntil = Time.time + 0.5f;
                    }
                    break;

                case FState.Hurt:
                    stateTimer -= dt;
                    vel = Vector2.MoveTowards(vel, Vector2.zero, 400f * dt);
                    Move(vel * dt);
                    if (stateTimer <= 0f) state = FState.Free;
                    break;

                case FState.Dead:
                    stateTimer -= dt;
                    if (stateTimer <= 0f && DestroyOnDeath) Destroy(gameObject);
                    break;

                case FState.Grabbed:
                    break;

                default:
                    Think(dt);
                    break;
            }
            if (flashTimer > 0f) flashTimer -= Time.unscaledDeltaTime;
        }

        void Land()
        {
            height = 0f;
            vel = Vector2.zero;
            if (flyingProjectile)
            {
                flyingProjectile = false;
                hp -= 10f;
            }
            Feel.Shake(1.5f, 0.1f);
            Fx.Debris(transform.position, Art.Hex("#cdb486"), 3);
            if (hp <= 0f)
            {
                Die();
            }
            else
            {
                state = FState.Down;
                stateTimer = 0.9f;
            }
        }

        protected virtual void LateUpdate()
        {
            if (visual == null) return;
            visual.localPosition = new Vector3(0f, height, 0f);

            float angle = 0f;
            if (state == FState.Down || state == FState.Dead) angle = 90f * facing;
            else if (state == FState.Airborne) angle = 50f * facing;
            visual.localRotation = Quaternion.Euler(0f, 0f, angle);

            // Arm pose tells the attack phase: pulled back on windup, stretched on the hit.
            if (current != null && state == FState.Attacking)
            {
                bool windup = attackT < current.windup;
                bool active = !windup && attackT < current.windup + current.active + 0.05f;
                float len = windup ? 5f : active ? current.armLength : 9f;
                arm.transform.localPosition = new Vector3(windup ? -bodySize.x * 0.5f : bodySize.x * 0.3f, bodySize.y * 0.62f, 0f);
                arm.transform.localScale = new Vector3(len, 4f, 1f);
                arm.transform.localPosition += new Vector3(len * 0.5f - 4f, 0f, 0f);
            }
            else
            {
                arm.transform.localPosition = new Vector3(bodySize.x * 0.3f, bodySize.y * 0.62f, 0f);
                arm.transform.localScale = new Vector3(8f, 4f, 1f);
            }

            bool flash = flashTimer > 0f;
            bool blink = state == FState.Dead && Mathf.Repeat(Time.time * 10f, 1f) > 0.5f;
            bool invuln = Time.time < invulnerableUntil && Mathf.Repeat(Time.time * 16f, 1f) > 0.5f;
            body.color = flash ? Color.white : bodyColor;
            head.color = flash ? Color.white : headColor;
            arm.color = flash ? Color.white : armColor;

            int order = Art.DepthOrder(transform.position.y);
            for (int i = 0; i < parts.Count; i++)
            {
                if (parts[i] == null) continue;
                parts[i].sortingOrder = order + partOffsets[i];
                parts[i].enabled = !blink && !(invuln && parts[i] != shadow);
            }
            if (shadow != null) shadow.transform.localScale = new Vector3((bodySize.x + 8f) * Mathf.Clamp(1f - height / 120f, 0.5f, 1f), 4f, 1f);
        }

        protected void SetArmColor(Color c) => armColor = c;
    }
}
