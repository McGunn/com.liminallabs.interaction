using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace LiminalLabs.Interaction
{
    /// <summary>
    /// In-game answer to "why can't I interact": F3 toggles a panel listing every
    /// active interactor — its detector, current focus, ranked candidates with
    /// scores, hold progress, and the last rejection reason. Drop it in any scene;
    /// it costs nothing while hidden.
    /// </summary>
    [AddComponentMenu("Liminal Labs/Interaction/Interaction Debug Overlay")]
    public class InteractionDebugOverlay : MonoBehaviour
    {
        [SerializeField] private bool startVisible = false;
        private bool visible;

        void Awake() => visible = startVisible;

        void Update()
        {
            if (TogglePressed()) visible = !visible;
        }

        private static bool TogglePressed()
        {
#if ENABLE_INPUT_SYSTEM
            Keyboard keyboard = Keyboard.current;
            return keyboard != null && keyboard.f3Key.wasPressedThisFrame;
#else
            return Input.GetKeyDown(KeyCode.F3);
#endif
        }

        void OnGUI()
        {
            if (!visible) return;

            var rect = new Rect(10, 10, 360, Screen.height - 20);
            GUILayout.BeginArea(rect, GUI.skin.box);
            GUILayout.Label("<b>Interaction</b>  (F3 to hide)", Rich());

            foreach (Interactor interactor in Interactor.Active)
            {
                GUILayout.Space(4);
                string detector = interactor.Detector != null ? interactor.Detector.GetType().Name : "NO DETECTOR";
                GUILayout.Label($"<b>{interactor.name}</b>  ·  {detector}", Rich());

                if (interactor.Focused != null)
                {
                    string hold = interactor.IsHolding ? $"  ·  hold {interactor.HoldProgress01:P0}" : "";
                    GUILayout.Label($"  focus: {interactor.Focused.DisplayName}{hold}", Rich());
                }
                else
                {
                    GUILayout.Label("  focus: —", Rich());
                }

                var candidates = interactor.Candidates;
                for (int i = 0; i < candidates.Count && i < 5; i++)
                {
                    GUILayout.Label($"    {candidates[i].score:0.00}  {candidates[i].interactable.DisplayName}  ({candidates[i].distance:0.0} m)", Rich());
                }

                if (interactor.LastRejection != InteractionRejection.None)
                {
                    GUILayout.Label($"  <color=#ffb060>last rejection: {interactor.LastRejection}</color>", Rich());
                }
            }
            GUILayout.EndArea();
        }

        private static GUIStyle rich;
        private static GUIStyle Rich()
        {
            if (rich == null) rich = new GUIStyle(GUI.skin.label) { richText = true, fontSize = 12 };
            return rich;
        }
    }
}
