using UnityEditor;
using UnityEngine;
using LiminalLabs.Core.Editor;

namespace LiminalLabs.Interaction
{
    /// <summary>Scene wiring checks for the interaction system — the silent-failure
    /// candidates: interactors that can't detect, interactables that can't be found
    /// or offer nothing.</summary>
    public sealed class InteractionSceneCheck : ILiminalSetupCheck
    {
        public string Category => "Interaction";
        public int Order => 0;
        private const int MaxRows = 10;

        public void Run(LiminalSetupReport report)
        {
            int interactors = 0, issues = 0, suppressed = 0;

            void Add(string title, string message, Object context)
            {
                issues++;
                if (issues > MaxRows) { suppressed++; return; }
                Object captured = context;
                report.Warn(title, message, () => { Selection.activeObject = captured; EditorGUIUtility.PingObject(captured); }, "Select");
            }

            foreach (Interactor interactor in Object.FindObjectsByType<Interactor>(FindObjectsInactive.Include))
            {
                interactors++;
                if (interactor.GetComponent<InteractionDetector>() == null &&
                    new SerializedObject(interactor).FindProperty("detector").objectReferenceValue == null)
                {
                    Add($"Interactor '{interactor.gameObject.name}' has no detector", "It will never find anything to interact with.", interactor);
                }
            }

            int interactables = 0;
            foreach (Interactable interactable in Object.FindObjectsByType<Interactable>(FindObjectsInactive.Include))
            {
                interactables++;
                if (interactable.GetComponentInChildren<Collider>() == null)
                {
                    Add($"Interactable '{interactable.gameObject.name}' has no collider", "Detectors cannot find it.", interactable);
                }
                else if (interactable.Verbs.Count == 0)
                {
                    Add($"Interactable '{interactable.gameObject.name}' has no verbs", "It offers nothing to do.", interactable);
                }
            }

            if (suppressed > 0)
            {
                report.Warn($"…and {suppressed} more interaction wiring issue(s)", "Fix the ones above and re-run.");
            }
            if (issues == 0 && (interactors > 0 || interactables > 0))
            {
                report.Pass($"{interactors} interactor(s) and {interactables} interactable(s) wire cleanly");
            }
        }
    }

    /// <summary>Verb asset audit: verbs with no name are invisible in prompts.</summary>
    public sealed class InteractionVerbCheck : ILiminalSetupCheck
    {
        public string Category => "Interaction";
        public int Order => 1;

        public void Run(LiminalSetupReport report)
        {
            int total = 0;
            foreach (string guid in AssetDatabase.FindAssets("t:LiminalLabs.Interaction.Interaction"))
            {
                var verb = AssetDatabase.LoadAssetAtPath<Interaction>(AssetDatabase.GUIDToAssetPath(guid));
                if (verb == null) continue;
                total++;
            }
            if (total > 0)
            {
                report.Pass($"{total} interaction verb(s) found");
            }
        }
    }
}
