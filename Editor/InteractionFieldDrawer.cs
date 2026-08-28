using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;
using LiminalLabs.Core.Editor;

namespace LiminalLabs.Interaction
{
    /// <summary>Verb fields draw as the shared searchable dropdown (descriptions as
    /// tooltips, ping, Create New… in place) — same picking experience as game
    /// events and audio cues.</summary>
    [CustomPropertyDrawer(typeof(Interaction), true)]
    public class InteractionFieldDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);
            Rect field = EditorGUI.PrefixLabel(position, label);
            var current = property.objectReferenceValue as Interaction;

            var dropRect = new Rect(field.x, field.y, field.width - 24f, field.height);
            var pingRect = new Rect(field.xMax - 22f, field.y, 22f, field.height);

            string display = current != null ? current.name : "None";
            string tooltip = current != null && !string.IsNullOrEmpty(current.Description)
                ? current.Description
                : "Pick an interaction verb (type to search), or create one in place.";
            if (EditorGUI.DropdownButton(dropRect, new GUIContent(display, tooltip), FocusType.Keyboard))
            {
                SerializedObject serializedObject = property.serializedObject;
                string path = property.propertyPath;
                new LiminalAssetDropdown(new AdvancedDropdownState(),
                    LiminalAssetDropdown.FieldAssetType(fieldInfo, typeof(Interaction)),
                    picked => LiminalAssetDropdown.Assign(serializedObject, path, picked),
                    type => type == typeof(Interaction) ? "Interaction Verb" : type.Name).Show(dropRect);
            }

            using (new EditorGUI.DisabledScope(current == null))
            {
                if (GUI.Button(pingRect, new GUIContent("◎", "Ping the verb asset"), EditorStyles.miniButton))
                {
                    EditorGUIUtility.PingObject(current);
                }
            }
            EditorGUI.EndProperty();
        }
    }
}
