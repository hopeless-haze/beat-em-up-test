using System.Collections.Generic;
using UnityEngine;

namespace Bogatyri.Prototype
{
    // Something lying on the road: a cart pole (оглобля) to fight with, or a barrel to throw.
    public class Pickup : MonoBehaviour
    {
        public enum Kind { Pole, Barrel }

        public static readonly List<Pickup> All = new List<Pickup>();
        public Kind kind;

        void OnEnable() => All.Add(this);
        void OnDisable() => All.Remove(this);

        public static Pickup Create(Transform parent, Kind kind, Vector3 pos)
        {
            var go = new GameObject(kind == Kind.Pole ? "Оглобля" : "Бочка");
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            var p = go.AddComponent<Pickup>();
            p.kind = kind;
            int order = Art.DepthOrder(pos.y);
            if (kind == Kind.Pole)
            {
                Art.Box(go.transform, "Shadow", new Vector2(0, -1), new Vector2(32, 3), new Color(0, 0, 0, 0.25f), order - 1);
                Art.Box(go.transform, "Wood", Vector2.zero, new Vector2(30, 3), Art.Hex("#8e5f35"), order);
            }
            else
            {
                BarrelBody(go.transform, order);
            }
            return p;
        }

        public static void BarrelBody(Transform parent, int order)
        {
            Art.Box(parent, "Shadow", new Vector2(0, -2), new Vector2(18, 4), new Color(0, 0, 0, 0.25f), order - 1);
            Art.Box(parent, "Staves", Vector2.zero, new Vector2(14, 16), Art.Hex("#8e5f35"), order);
            Art.Box(parent, "Hoop1", new Vector2(0, 3), new Vector2(16, 2), Art.Hex("#55595e"), order + 1);
            Art.Box(parent, "Hoop2", new Vector2(0, 11), new Vector2(16, 2), Art.Hex("#55595e"), order + 1);
        }

        public static Pickup Nearest(Vector3 from, float maxDx, float maxDy)
        {
            Pickup best = null;
            float bestDist = float.MaxValue;
            foreach (var p in All)
            {
                var d = p.transform.position - from;
                if (Mathf.Abs(d.x) > maxDx || Mathf.Abs(d.y) > maxDy) continue;
                if (d.sqrMagnitude < bestDist)
                {
                    best = p;
                    bestDist = d.sqrMagnitude;
                }
            }
            return best;
        }
    }

    // A barrel in the hero's hands or in flight.
    public class Prop : MonoBehaviour
    {
        const float Gravity = 420f;

        Transform visual;
        SpriteRenderer[] renderers;
        bool flying;
        float vx, height, heightVel;
        Team team;
        readonly HashSet<Fighter> alreadyHit = new HashSet<Fighter>();

        public static Prop CreateBarrel(Transform parent, Vector3 pos)
        {
            var go = new GameObject("Бочка в руках");
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            var prop = go.AddComponent<Prop>();
            prop.visual = new GameObject("Visual").transform;
            prop.visual.SetParent(go.transform, false);
            Pickup.BarrelBody(prop.visual, 0);
            prop.renderers = go.GetComponentsInChildren<SpriteRenderer>();
            return prop;
        }

        public void Carry(Fighter holder)
        {
            transform.position = holder.transform.position;
            visual.localPosition = new Vector3(0f, holder.height + 50f, 0f);
            SetOrder(Art.DepthOrder(holder.transform.position.y) + 6);
        }

        public void Throw(int dir, Team thrower)
        {
            flying = true;
            team = thrower;
            vx = dir * 210f;
            height = 44f;
            heightVel = 30f;
        }

        void Update()
        {
            if (!flying) return;
            float dt = Time.deltaTime;
            heightVel -= Gravity * dt;
            height += heightVel * dt;
            transform.position += new Vector3(vx * dt, 0f, 0f);
            visual.localPosition = new Vector3(0f, Mathf.Max(height, 0f), 0f);
            SetOrder(Art.DepthOrder(transform.position.y));

            int hits = Combat.FlyingHits(transform.position, height, vx, team, alreadyHit, null, 18f);
            if (hits > 0 || height <= 0f) Break();
        }

        public void Break()
        {
            Fx.Debris(transform.position + new Vector3(0f, height + 8f), Art.Hex("#8e5f35"), 8);
            Feel.Shake(1.5f, 0.1f);
            Destroy(gameObject);
        }

        void SetOrder(int order)
        {
            foreach (var r in renderers)
            {
                if (r == null) continue;
                r.sortingOrder = order + (r.name.StartsWith("Hoop") ? 1 : r.name == "Shadow" ? -1 : 0);
            }
        }
    }
}
