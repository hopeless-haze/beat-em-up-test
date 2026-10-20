using UnityEngine;

namespace Bogatyri.Prototype
{
    // Placeholder art: every shape in the prototype is a tinted 1x1 pixel sprite.
    // One world unit equals one game pixel; the screen is 384x216 units.
    public static class Art
    {
        static Sprite pixel;

        public static Sprite Pixel
        {
            get
            {
                if (pixel == null)
                {
                    var tex = new Texture2D(1, 1) { filterMode = FilterMode.Point };
                    tex.SetPixel(0, 0, Color.white);
                    tex.Apply();
                    // Pivot at bottom centre, so a box grows upward from where it stands.
                    pixel = Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0f), 1f);
                }
                return pixel;
            }
        }

        public static SpriteRenderer Box(Transform parent, string name, Vector2 localPos, Vector2 size, Color color, int order = 0)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = new Vector3(size.x, size.y, 1f);
            var r = go.AddComponent<SpriteRenderer>();
            r.sprite = Pixel;
            r.color = color;
            r.sortingOrder = order;
            return r;
        }

        // Sorting by depth: lower on screen means closer to the player, so drawn on top.
        public static int DepthOrder(float y) => Mathf.RoundToInt(-y * 10f);

        public static Color Hex(string hex)
        {
            ColorUtility.TryParseHtmlString(hex, out var c);
            return c;
        }
    }

    // Short-lived effect that ignores hitstop, since hitstop sets timeScale to zero.
    public class Fx : MonoBehaviour
    {
        public float life = 0.1f;
        public Vector2 velocity;

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            life -= dt;
            transform.position += (Vector3)(velocity * dt);
            if (life <= 0f) Destroy(gameObject);
        }

        public static void Spark(Vector3 at, float size)
        {
            var parent = Stage.I != null ? Stage.I.transform : null;
            var gold = Art.Hex("#e2b23c");
            var core = Art.Hex("#fff6c9");
            Spawn(parent, at + new Vector3(0, -size), new Vector2(3, size * 2 + 1), gold, 0.09f, Vector2.zero);
            Spawn(parent, at + new Vector3(0, -1.5f), new Vector2(size * 2 + 1, 3), gold, 0.09f, Vector2.zero);
            Spawn(parent, at + new Vector3(0, -2.5f), new Vector2(5, 5), core, 0.07f, Vector2.zero);
        }

        public static void Debris(Vector3 at, Color color, int count)
        {
            var parent = Stage.I != null ? Stage.I.transform : null;
            for (int i = 0; i < count; i++)
            {
                var v = new Vector2(Random.Range(-90f, 90f), Random.Range(30f, 110f));
                Spawn(parent, at, new Vector2(3, 3), color, Random.Range(0.25f, 0.45f), v);
            }
        }

        static void Spawn(Transform parent, Vector3 at, Vector2 size, Color c, float life, Vector2 v)
        {
            var r = Art.Box(parent, "Fx", at, size, c, 30000);
            var fx = r.gameObject.AddComponent<Fx>();
            fx.life = life;
            fx.velocity = v;
        }
    }
}
