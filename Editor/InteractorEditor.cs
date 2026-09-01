using UnityEditor;
using UnityEngine;

namespace LiminalLabs.Interaction
{
    /// <summary>Interactor inspector: in play mode, the live view of what this agent
    /// sees — detector, focus, ranked candidates with scores, hold progress, and the
    /// last rejection reason — so "why can't I interact" is answered right here.</summary>
    [CustomEditor(typeof(Interactor))]
    public class InteractorEditor : UnityEditor.Editor
    {
        public override bool RequiresConstantRepaint() => EditorApplication.isPlaying;

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            var interactor = (Interactor)target;
            if (!EditorApplication.isPlaying)
            {
                if (interactor.GetComponent<InteractionDetector>() == null &&
                    serializedObject.FindProperty("detector").objectReferenceValue == null)
                {
                    EditorGUILayout.HelpBox("No detector — add a Ray, Proximity, or Pointer Detector (or assign one) or this interactor finds nothing.", MessageType.Warning);
                }
                return;
            }

            EditorGUILayout.Space(6);
            GUILayout.Label("Live", EditorStyles.boldLabel);
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                GUILayout.Label(interactor.Focused != null
                    ? $"Focus: {interactor.Focused.DisplayName}"
                    : "Focus: —", EditorStyles.miniLabel);

                foreach (InteractionCandidate candidate in interactor.Candidates)
                {
                    GUILayout.Label($"    {candidate.score:0.00}  {candidate.interactable.DisplayName}  ·  {candidate.distance:0.0} m", EditorStyles.miniLabel);
                }

                if (interactor.IsHolding)
                {
                    Rect rect = EditorGUILayout.GetControlRect(false, 14);
                    EditorGUI.ProgressBar(rect, interactor.HoldProgress01, "holding…");
                }
                if (interactor.LastRejection != InteractionRejection.None)
                {
                    string blocker = interactor.LastBlocker != null
                        ? $"\nRefused by {Interactor.Describe(interactor.LastBlocker)}."
                        : "";
                    EditorGUILayout.HelpBox($"Last rejection: {interactor.LastRejection}{blocker}", MessageType.Warning);
                    if (interactor.LastBlocker is Component component && component != null &&
                        GUILayout.Button("Select the condition", EditorStyles.miniButton))
                    {
                        Selection.activeObject = component;
                        EditorGUIUtility.PingObject(component);
                    }
                }
            }
        }
    }
}
