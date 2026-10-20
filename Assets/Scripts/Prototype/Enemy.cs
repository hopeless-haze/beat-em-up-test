using UnityEngine;

namespace Bogatyri.Prototype
{
    public enum EnemyKind
    {
        Club,     // дубинщик: slow, hits hard and knocks down
        Flail,    // кистенёк: fast, weak, interrupts
    }

    public class Enemy : Fighter
    {
        // At most this many robbers press the attack at once; the rest circle around, as in the genre classics.
        const int MaxAttackers = 2;
        static int attackersNow;

        public EnemyKind kind;
        float speed;
        AttackDef attack;
        float cooldown;
        bool engaging;
        float laneOffset;
        float rethinkTimer;

        public static void ResetTokens() => attackersNow = 0;

        public static Enemy Create(Transform parent, EnemyKind kind, Vector3 pos)
        {
            var go = new GameObject(kind == EnemyKind.Club ? "Дубинщик" : "Кистенёк");
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            var e = go.AddComponent<Enemy>();
            e.team = Team.Enemies;
            e.kind = kind;
            e.facing = -1;
            if (kind == EnemyKind.Club)
            {
                e.displayName = "Дубинщик";
                e.maxHp = 60f;
                e.speed = 34f;
                e.attack = new AttackDef { name = "Дубиной", windup = 0.55f, active = 0.1f, recovery = 0.5f, reachMin = 2, reachMax = 30, damage = 14, force = 140, knockdown = true, hitstop = 0.07f, armLength = 24 };
                e.BuildBody(new Vector2(20, 36), Art.Hex("#7d6247"), Art.Hex("#b3312a"), Art.Hex("#8e5f35"));
            }
            else
            {
                e.displayName = "Кистенёк";
                e.maxHp = 35f;
                e.speed = 60f;
                e.attack = new AttackDef { name = "Кистенём", windup = 0.22f, active = 0.08f, recovery = 0.35f, reachMin = 2, reachMax = 22, damage = 6, force = 60, stun = 0.3f, hitstop = 0.04f, armLength = 16 };
                e.BuildBody(new Vector2(14, 32), Art.Hex("#9b7e5c"), Art.Hex("#b3312a"), Art.Hex("#55595e"));
            }
            e.cooldown = Random.Range(0.4f, 1.2f);
            e.laneOffset = Random.Range(-10f, 10f);
            return e;
        }

        protected override void Think(float dt)
        {
            if (state == FState.Attacking)
            {
                TickAttack(dt);
                return;
            }

            var p = Stage.I != null ? Stage.I.player : null;
            if (p == null || !p.IsAlive)
            {
                StopEngaging();
                return;
            }

            cooldown -= dt;
            rethinkTimer -= dt;
            if (rethinkTimer <= 0f)
            {
                rethinkTimer = Random.Range(1f, 2f);
                laneOffset = Random.Range(-10f, 10f);
                if (!engaging && attackersNow < MaxAttackers)
                {
                    engaging = true;
                    attackersNow++;
                }
            }

            var me = transform.position;
            var target = p.transform.position;
            Face(target.x - me.x);

            // Stand on our side of the hero: close if it is our turn, at a distance if not.
            float side = me.x <= target.x ? -1f : 1f;
            float distance = engaging ? attack.reachMax * 0.7f : 64f;
            var spot = new Vector3(target.x + side * distance, target.y + (engaging ? 0f : laneOffset * 2f));
            var to = spot - me;
            if (to.magnitude > 3f)
            {
                var step = new Vector2(Mathf.Clamp(to.x, -1f, 1f), Mathf.Clamp(to.y / 8f, -1f, 1f));
                Move(new Vector2(step.x * speed, step.y * speed * 0.7f) * dt);
            }

            float dx = Mathf.Abs(target.x - me.x);
            bool inRange = dx >= attack.reachMin && dx <= attack.reachMax * 0.95f && Mathf.Abs(target.y - me.y) < 6f;
            bool heroStanding = p.state != FState.Down && p.state != FState.Airborne && p.state != FState.Dead;
            if (engaging && inRange && heroStanding && cooldown <= 0f)
            {
                StartAttack(attack);
                cooldown = Random.Range(0.8f, 1.6f);
            }
        }

        protected override void OnAttackFinished(AttackDef a)
        {
            // Hand the turn to someone else now and then.
            if (Random.value < 0.5f) StopEngaging();
        }

        protected override void Interrupt()
        {
            base.Interrupt();
            StopEngaging();
        }

        void StopEngaging()
        {
            if (!engaging) return;
            engaging = false;
            attackersNow = Mathf.Max(0, attackersNow - 1);
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            StopEngaging();
        }
    }
}
