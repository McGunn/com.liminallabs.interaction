using UnityEngine;

namespace LiminalLabs.Interaction.Demo
{
    /// <summary>
    /// A reference presenter: a world-space prompt that follows whichever interactor
    /// is active, reading only public hooks — focus, verb, hold progress, last
    /// rejection. This is the pattern for building your own prompt/outline/cursor
    /// systems: the core never draws UI; presenters listen.
    /// </summary>
    [RequireComponent(typeof(TextMesh))]
    public class DemoInteractionPrompt : MonoBehaviour
    {
        [SerializeField] private Vector3 offset = new Vector3(0f, 1.1f, 0f);

        private TextMesh textMesh;

        void Awake() => textMesh = GetComponent<TextMesh>();

        void LateUpdate()
        {
            Interactor interactor = Interactor.Active.Count > 0 ? Interactor.Active[0] : null;
            Interactable focus = interactor != null ? interactor.Focused : null;

            if (focus == null)
            {
                textMesh.text = "";
                return;
            }

            Interaction verb = focus.PrimaryVerb;
            string verbName = verb != null ? verb.DisplayName : "?";
            string line = $"{verbName} — {focus.DisplayName}";

            var context = new InteractionContext(interactor, focus, verb, focus.InteractionPoint);
            if (interactor.Validate(context) == InteractionRejection.VerbUnavailable)
            {
                line += "  (locked)";
            }
            else if (interactor.IsHolding)
            {
                line += $"  {Mathf.RoundToInt(interactor.HoldProgress01 * 100)}%";
            }
            else if (verb != null && verb.HoldSeconds > 0f)
            {
                line += "  (hold)";
            }

            textMesh.text = line;
            transform.position = focus.InteractionPoint + offset;

            Camera cam = Camera.main;
            if (cam != null)
            {
                transform.rotation = Quaternion.LookRotation(transform.position - cam.transform.position);
            }
        }
    }
}
