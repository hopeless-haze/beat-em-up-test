using System.Collections.Generic;
using UnityEngine;

namespace Bogatyri.Prototype
{
    public enum Team { Heroes, Enemies }

    public struct Hit
    {
        public float damage;
        public bool knockdown;
        public float force;      // knockback speed, px/s
        public float stun;       // seconds of hurt state
        public bool hitsDowned;  // can hit an enemy lying on the ground
        public int dir;          // push direction; 0 means "away from the attacker"
        public Fighter from;
    }

    [System.Serializable]
    public class AttackDef
    {
        public string name;
        public float windup, active, recovery;
        public float reachMin, reachMax, depth = 9f;
        public float damage, force, stun = 0.25f, hitstop = 0.04f;
        public bool knockdown, hitsDowned;
        public float armLength = 14f;

        public float Total => windup + active + recovery;
    }

    public static class Combat
    {
        static readonly List<Fighter> buffer = new List<Fighter>();

        static List<Fighter> Snapshot()
        {
            buffer.Clear();
            buffer.AddRange(Fighter.All);
            return buffer;
        }

        // Hits everyone of the other team inside a box in front of the attacker.
        public static int HitArea(Fighter a, AttackDef d, float extraReach = 0f, float damageMul = 1f)
        {
            int count = 0;
            var ap = a.transform.position;
            foreach (var t in Snapshot())
            {
                if (t == null || t.team == a.team || !t.IsAlive) continue;
                var tp = t.transform.position;
                float ahead = (tp.x - ap.x) * a.facing;
                if (ahead < d.reachMin || ahead > d.reachMax + extraReach) continue;
                if (Mathf.Abs(tp.y - ap.y) > d.depth) continue;
                if (t.height > 24f) continue;

                var hit = new Hit
                {
                    damage = d.damage * damageMul,
                    knockdown = d.knockdown,
                    force = d.force,
                    stun = d.stun,
                    hitsDowned = d.hitsDowned,
                    from = a,
                };
                if (t.ReceiveHit(hit))
                {
                    count++;
                    Fx.Spark(tp + new Vector3(-a.facing * 5f, 26f + t.height), d.knockdown ? 7f : 5f);
                }
            }
            if (count > 0)
            {
                Feel.Hitstop(d.hitstop);
                Feel.Shake(d.knockdown ? 2f : 1f, 0.08f);
            }
            return count;
        }

        // Something flying through the air (a thrown robber or a barrel) knocks down whoever it touches.
        public static int FlyingHits(Vector3 pos, float height, float vx, Team team, HashSet<Fighter> already, Fighter self, float damage)
        {
            if (height > 36f) return 0;
            int count = 0;
            foreach (var t in Snapshot())
            {
                if (t == null || t == self || t.team == team || !t.IsAlive || already.Contains(t)) continue;
                if (t.state == FState.Airborne || t.state == FState.Down) continue;
                var tp = t.transform.position;
                if (Mathf.Abs(tp.x - pos.x) > 14f || Mathf.Abs(tp.y - pos.y) > 9f) continue;
                already.Add(t);
                var hit = new Hit { damage = damage, knockdown = true, force = 140f, dir = vx >= 0 ? 1 : -1 };
                if (t.ReceiveHit(hit))
                {
                    count++;
                    Fx.Spark(tp + new Vector3(0, 26f), 7f);
                }
            }
            if (count > 0)
            {
                Feel.Hitstop(0.08f);
                Feel.Shake(2.5f, 0.12f);
            }
            return count;
        }

        public static Fighter FindInFront(Fighter a, float min, float max, float depth, System.Predicate<Fighter> match)
        {
            Fighter best = null;
            float bestDist = float.MaxValue;
            var ap = a.transform.position;
            foreach (var t in Fighter.All)
            {
                if (t == null || t == a || t.team == a.team || !match(t)) continue;
                var tp = t.transform.position;
                float ahead = (tp.x - ap.x) * a.facing;
                if (ahead < min || ahead > max || Mathf.Abs(tp.y - ap.y) > depth) continue;
                if (ahead < bestDist)
                {
                    best = t;
                    bestDist = ahead;
                }
            }
            return best;
        }
    }
}
