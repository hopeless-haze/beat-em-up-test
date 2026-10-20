using UnityEngine;

namespace Bogatyri.Prototype
{
    // Hitstop and screen shake: the two cheapest tricks that make a blow feel heavy.
    public static class Feel
    {
        static float stopUntil;
        static float shakeAmp, shakeUntil;

        public static void Hitstop(float seconds)
        {
            if (seconds <= 0f) return;
            stopUntil = Mathf.Max(stopUntil, Time.unscaledTime + seconds);
            Time.timeScale = 0f;
        }

        public static void Shake(float amplitude, float seconds)
        {
            shakeAmp = Mathf.Max(amplitude, Time.unscaledTime < shakeUntil ? shakeAmp : 0f);
            shakeUntil = Mathf.Max(shakeUntil, Time.unscaledTime + seconds);
        }

        // Called once per frame by the stage.
        public static Vector2 Tick()
        {
            if (Time.timeScale == 0f && Time.unscaledTime >= stopUntil) Time.timeScale = 1f;
            if (Time.unscaledTime >= shakeUntil) return Vector2.zero;
            return new Vector2(Random.Range(-1f, 1f), Random.Range(-1f, 1f)) * shakeAmp;
        }

        public static void Reset()
        {
            stopUntil = shakeUntil = 0f;
            Time.timeScale = 1f;
        }
    }
}
