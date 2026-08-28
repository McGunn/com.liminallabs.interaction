using UnityEditor;
using UnityEngine;

namespace LiminalLabs.Interaction
{
    /// <summary>Interactable inspector: wiring hints in edit mode (collider, verbs,
    /// range handle in the scene view), and the live verb/condition picture in play
    /// mode.</summary>
    [CustomEditor(typeof(Interactable))]
    [CanEditMultipleObjects]
    public class InteractableEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            if (targets.Length != 1) return;
            var interactable = (Interactable)target;

            if (interactable.GetComponentInChildren<Collider>() == null)
            {
                EditorGUILayout.HelpBox("No collider on this object or its children — detectors cannot find it.", MessageType.Warning);
            }
            if (interactable.Verbs.Count == 0)
            {
                EditorGUILayout.HelpBox("No verbs — this interactable offers nothing. Add at least one Interaction verb.", MessageType.Warning);
            }
            else
            {
                Interaction primary = interactable.PrimaryVerb;
                if (primary != null)
                {
                    string hold = primary.HoldSeconds > 0f ? $", hold {primary.HoldSeconds:0.##}s" : "";
                    EditorGUILayout.LabelField($"Primary verb: {primary.DisplayName}{hold}", EditorStyles.miniLabel);
                }
            }
        }

        private void OnSceneGUI()
        {
            var interactable = (Interactable)target;
            SerializedProperty range = serializedObject.FindProperty("rangeOverride");
            if (range.floatValue <= 0f) return;

            serializedObject.Update();
            Handles.color = new Color(0.4f, 0.8f, 0.4f, 0.7f);
            EditorGUI.BeginChangeCheck();
            float newRange = Handles.RadiusHandle(Quaternion.identity, interactable.InteractionPoint, range.floatValue);
            if (EditorGUI.EndChangeCheck())
            {
                range.floatValue = Mathf.Max(0f, newRange);
                serializedObject.ApplyModifiedProperties();
            }
        }
    }
}
