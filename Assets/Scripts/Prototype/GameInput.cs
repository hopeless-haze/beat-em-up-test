using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Bogatyri.Prototype
{
    // Keyboard: WASD or arrows to walk, J light, K heavy, L grab / pick up / throw, R restart.
    // Gamepad: left stick or d-pad, X light, Y heavy, B grab, Start restart.
    // Works with either the Input System package or the old Input Manager, whichever the project has active.
    public static class GameInput
    {
        public static Vector2 Move()
        {
            var v = Vector2.zero;
#if ENABLE_INPUT_SYSTEM
            var k = Keyboard.current;
            if (k != null)
            {
                if (k.aKey.isPressed || k.leftArrowKey.isPressed) v.x -= 1;
                if (k.dKey.isPressed || k.rightArrowKey.isPressed) v.x += 1;
                if (k.sKey.isPressed || k.downArrowKey.isPressed) v.y -= 1;
                if (k.wKey.isPressed || k.upArrowKey.isPressed) v.y += 1;
            }
            var g = Gamepad.current;
            if (g != null)
            {
                var stick = g.leftStick.ReadValue();
                if (stick.sqrMagnitude > 0.05f) v += stick;
                v += g.dpad.ReadValue();
            }
#else
            if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow)) v.x -= 1;
            if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) v.x += 1;
            if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow)) v.y -= 1;
            if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow)) v.y += 1;
#endif
            return Vector2.ClampMagnitude(v, 1f);
        }

#if ENABLE_INPUT_SYSTEM
        static bool Pressed(Key key, System.Func<Gamepad, UnityEngine.InputSystem.Controls.ButtonControl> pad)
        {
            var k = Keyboard.current;
            if (k != null && k[key].wasPressedThisFrame) return true;
            var g = Gamepad.current;
            return g != null && pad(g).wasPressedThisFrame;
        }

        public static bool Light => Pressed(Key.J, g => g.buttonWest);
        public static bool Heavy => Pressed(Key.K, g => g.buttonNorth);
        public static bool Grab => Pressed(Key.L, g => g.buttonEast);
        public static bool Restart => Pressed(Key.R, g => g.startButton);
#else
        public static bool Light => Input.GetKeyDown(KeyCode.J);
        public static bool Heavy => Input.GetKeyDown(KeyCode.K);
        public static bool Grab => Input.GetKeyDown(KeyCode.L);
        public static bool Restart => Input.GetKeyDown(KeyCode.R);
#endif
    }
}
