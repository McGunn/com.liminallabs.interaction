using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace LiminalLabs.Interaction.Demo
{
    /// <summary>Demo-local input polling (interact press/hold/release, click, Tab)
    /// that works on either input backend — demo code never references one directly.</summary>
    internal static class DemoRigInput
    {
        public static bool InteractPressed
        {
            get
            {
#if ENABLE_INPUT_SYSTEM
                Keyboard k = Keyboard.current;
                return k != null && k.eKey.wasPressedThisFrame;
#else
                return Input.GetKeyDown(KeyCode.E);
#endif
            }
        }

        public static bool InteractReleased
        {
            get
            {
#if ENABLE_INPUT_SYSTEM
                Keyboard k = Keyboard.current;
                return k != null && k.eKey.wasReleasedThisFrame;
#else
                return Input.GetKeyUp(KeyCode.E);
#endif
            }
        }

        public static bool ClickPressed
        {
            get
            {
#if ENABLE_INPUT_SYSTEM
                Mouse m = Mouse.current;
                return m != null && m.leftButton.wasPressedThisFrame;
#else
                return Input.GetMouseButtonDown(0);
#endif
            }
        }

        public static bool ClickReleased
        {
            get
            {
#if ENABLE_INPUT_SYSTEM
                Mouse m = Mouse.current;
                return m != null && m.leftButton.wasReleasedThisFrame;
#else
                return Input.GetMouseButtonUp(0);
#endif
            }
        }

        public static bool NextRigPressed
        {
            get
            {
#if ENABLE_INPUT_SYSTEM
                Keyboard k = Keyboard.current;
                return k != null && k.tabKey.wasPressedThisFrame;
#else
                return Input.GetKeyDown(KeyCode.Tab);
#endif
            }
        }
    }
}
