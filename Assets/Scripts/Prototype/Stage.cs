using System.Collections.Generic;
using UnityEngine;

namespace Bogatyri.Prototype
{
    // The road from the village: camera that only scrolls forward, waves of robbers that lock the screen,
    // a "ВПЕРЁД" sign when a wave is cleared, and a placeholder HUD.
    public class Stage : MonoBehaviour
    {
        public static Stage I { get; private set; }

        // Walkable band of road in world pixels. The screen spans y from -108 to 108.
        public const float MinY = -100f;
        public const float MaxY = -32f;
        const float HalfWidth = 192f;
        const float Length = 1800f;

        struct Wave
        {
            public float triggerX;
            public EnemyKind[] right, left;
        }

        readonly List<Wave> waves = new List<Wave>
        {
            new Wave { triggerX = 240, right = new[] { EnemyKind.Flail, EnemyKind.Flail }, left = new EnemyKind[0] },
            new Wave { triggerX = 640, right = new[] { EnemyKind.Club, EnemyKind.Flail }, left = new[] { EnemyKind.Flail } },
            new Wave { triggerX = 1060, right = new[] { EnemyKind.Club, EnemyKind.Flail, EnemyKind.Flail }, left = new[] { EnemyKind.Club } },
            new Wave { triggerX = 1480, right = new[] { EnemyKind.Club, EnemyKind.Club, EnemyKind.Flail }, left = new[] { EnemyKind.Flail, EnemyKind.Flail } },
        };

        public Player player;
        Camera cam;
        float camX;
        bool locked;
        int waveIndex;
        float goSignUntil;
        bool won;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            // The prototype builds itself in whatever scene is open, so pressing Play is enough.
            if (FindFirstObjectByType<Stage>() == null) Create();
        }

        static void Create()
        {
            new GameObject("Prototype").AddComponent<Stage>();
        }

        void Awake()
        {
            I = this;
            Application.targetFrameRate = 60;
            Feel.Reset();
            Enemy.ResetTokens();
        }

        void Start()
        {
            SetUpCamera();
            BuildRoad();
            player = Player.Create(transform, new Vector3(60f, -66f, 0f));
            Pickup.Create(transform, Pickup.Kind.Pole, new Vector3(520f, -80f, 0f));
            Pickup.Create(transform, Pickup.Kind.Barrel, new Vector3(960f, -48f, 0f));
            Pickup.Create(transform, Pickup.Kind.Barrel, new Vector3(1380f, -86f, 0f));
            camX = HalfWidth;
        }

        void SetUpCamera()
        {
            cam = Camera.main;
            if (cam == null)
            {
                var go = new GameObject("Main Camera") { tag = "MainCamera" };
                cam = go.AddComponent<Camera>();
            }
            cam.orthographic = true;
            cam.orthographicSize = 108f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Art.Hex("#94c6da");
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 100f;
        }

        void BuildRoad()
        {
            var bg = new GameObject("Дорога").transform;
            bg.SetParent(transform, false);
            float w = Length + 800f, cx = Length / 2f;
            Art.Box(bg, "Небо верх", new Vector2(cx, 60), new Vector2(w, 60), Art.Hex("#79b3cf"), -20000);
            Art.Box(bg, "Лес", new Vector2(cx, -18), new Vector2(w, 40), Art.Hex("#3f6b4a"), -19990);
            Art.Box(bg, "Трава", new Vector2(cx, -30), new Vector2(w, 12), Art.Hex("#6d9a4f"), -19980);
            Art.Box(bg, "Дорога", new Vector2(cx, -108), new Vector2(w, 80), Art.Hex("#b89a6a"), -19970);
            Art.Box(bg, "Колея", new Vector2(cx, -80), new Vector2(w, 2), Art.Hex("#a7895c"), -19960);
            Art.Box(bg, "Колея", new Vector2(cx, -50), new Vector2(w, 2), Art.Hex("#a7895c"), -19960);
            for (float x = -400f; x < Length + 400f; x += 48f)
            {
                // Fence posts and tree tops so the scrolling reads even with flat colours.
                Art.Box(bg, "Кол", new Vector2(x, -30), new Vector2(3, 20), Art.Hex("#5a3f26"), -19950);
                Art.Box(bg, "Жердь", new Vector2(x + 24, -22), new Vector2(48, 2), Art.Hex("#76583a"), -19951);
                Art.Box(bg, "Крона", new Vector2(x + 12, 2), new Vector2(26, 10 + Mathf.Repeat(x * 0.37f, 9f)), Art.Hex("#365f41"), -19985);
            }
            Art.Box(bg, "Столб «Киевъ»", new Vector2(Length - 60f, -30), new Vector2(4, 34), Art.Hex("#6b4423"), -19940);
            Art.Box(bg, "Табличка", new Vector2(Length - 60f, -2), new Vector2(30, 9), Art.Hex("#8e5f35"), -19939);
        }

        public Vector3 ClampToScreen(Vector3 p)
        {
            p.x = Mathf.Clamp(p.x, camX - HalfWidth + 10f, Mathf.Min(camX + HalfWidth - 10f, Length));
            return p;
        }

        void Update()
        {
            var shake = Feel.Tick();

            if (GameInput.Restart)
            {
                Restart();
                return;
            }
            if (player == null) return;

            // Camera only moves forward, and stops while a wave is on screen.
            if (!locked) camX = Mathf.Clamp(Mathf.Max(camX, player.transform.position.x - 40f), HalfWidth, Length - HalfWidth);
            cam.transform.position = new Vector3(Mathf.Round(camX) + shake.x, shake.y, -10f);

            int enemiesLeft = CountEnemies();
            if (!locked && waveIndex < waves.Count && player.transform.position.x >= waves[waveIndex].triggerX)
            {
                locked = true;
                SpawnWave(waves[waveIndex]);
            }
            else if (locked && enemiesLeft == 0)
            {
                locked = false;
                waveIndex++;
                goSignUntil = Time.time + 3f;
            }

            if (!won && waveIndex >= waves.Count && enemiesLeft == 0 && player.transform.position.x > Length - 120f) won = true;
        }

        void SpawnWave(Wave wave)
        {
            for (int i = 0; i < wave.right.Length; i++)
            {
                var pos = new Vector3(camX + HalfWidth + 20f + i * 26f, Random.Range(MinY + 6f, MaxY - 6f), 0f);
                Enemy.Create(transform, wave.right[i], pos);
            }
            for (int i = 0; i < wave.left.Length; i++)
            {
                var pos = new Vector3(camX - HalfWidth - 20f - i * 26f, Random.Range(MinY + 6f, MaxY - 6f), 0f);
                Enemy.Create(transform, wave.left[i], pos);
            }
        }

        static int CountEnemies()
        {
            int n = 0;
            foreach (var f in Fighter.All) if (f != null && f.team == Team.Enemies && f.IsAlive) n++;
            return n;
        }

        void Restart()
        {
            Feel.Reset();
            Destroy(gameObject);
            Create();
        }

        void OnDestroy()
        {
            if (I == this) I = null;
        }

        // ---------- placeholder HUD ----------

        GUIStyle label, big;

        void OnGUI()
        {
            if (player == null) return;
            // Draw in game pixels: 216 units tall, whatever the window size.
            float scale = Screen.height / 216f;
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
            float screenW = Screen.width / scale;
            if (label == null)
            {
                label = new GUIStyle(GUI.skin.label) { fontSize = 7, fontStyle = FontStyle.Bold };
                label.normal.textColor = Color.white;
                big = new GUIStyle(label) { fontSize = 14, alignment = TextAnchor.MiddleCenter };
            }

            // Hero panel: name, health, combo counter.
            Fill(4, 4, 104, 22, new Color(0.06f, 0.08f, 0.07f, 0.8f));
            GUI.Label(new Rect(8, 4, 100, 10), player.displayName, label);
            Fill(8, 15, 96, 5, Art.Hex("#3a1614"));
            Fill(8, 15, 96 * Mathf.Clamp01(player.hp / player.maxHp), 5, Art.Hex("#d2453a"));
            if (player.HasPole) GUI.Label(new Rect(60, 4, 50, 10), "оглобля", label);

            if (player.comboHits >= 2 && Time.time - player.lastLandedTime < 1.5f)
            {
                GUI.Label(new Rect(8, 28, 120, 12), player.comboHits + " УДАРОВ", label);
            }

            // Bar over the enemy you last hit, for two seconds.
            var t = player.lastTarget;
            if (t != null && t.IsAlive && Time.time - t.lastHurtTime < 2f)
            {
                Fill(screenW - 108, 4, 104, 22, new Color(0.06f, 0.08f, 0.07f, 0.8f));
                GUI.Label(new Rect(screenW - 104, 4, 100, 10), t.displayName, label);
                Fill(screenW - 104, 15, 96, 5, Art.Hex("#3a1614"));
                Fill(screenW - 104, 15, 96 * Mathf.Clamp01(t.hp / t.maxHp), 5, Art.Hex("#d2453a"));
            }

            bool showGo = !locked && waveIndex < waves.Count && (Time.time < goSignUntil || waveIndex == 0) && Mathf.Repeat(Time.time * 2f, 1f) > 0.3f;
            if (showGo) GUI.Label(new Rect(screenW - 90, 100, 86, 16), "ВПЕРЁД →", big);

            if (!player.IsAlive) GUI.Label(new Rect(0, 80, screenW, 30), "Сидор пал.  R — заново", big);
            if (won) GUI.Label(new Rect(0, 80, screenW, 30), "Дорога на Киев чиста!  R — ещё раз", big);

            GUI.Label(new Rect(6, 200, screenW, 12),
                "WASD — ходить   J — удар   K — тяжёлый / по лежачему   L — схватить, подобрать, бросить   R — заново", label);
        }

        static void Fill(float x, float y, float w, float h, Color c)
        {
            var old = GUI.color;
            GUI.color = c;
            GUI.DrawTexture(new UnityEngine.Rect(x, y, w, h), Texture2D.whiteTexture);
            GUI.color = old;
        }
    }
}
