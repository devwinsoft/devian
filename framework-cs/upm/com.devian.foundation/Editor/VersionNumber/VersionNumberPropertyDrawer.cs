// SSOT: skills/devian-unity/10-foundation/31-version-number-drawer/SKILL.md
// PropertyDrawer for VersionNumber - displays Major.Minor.Patch as 3 IntFields

using UnityEditor;
using UnityEngine;

namespace Devian
{
    [CustomPropertyDrawer(typeof(VersionNumber))]
    public class VersionNumberPropertyDrawer : PropertyDrawer
    {
        private const float ComponentSpacing = 2f;

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);

            var majorProp = property.FindPropertyRelative("Major");
            var minorProp = property.FindPropertyRelative("Minor");
            var patchProp = property.FindPropertyRelative("Patch");

            if (majorProp == null || minorProp == null || patchProp == null)
            {
                EditorGUI.LabelField(position, label.text, "(VersionNumber: missing fields)");
                EditorGUI.EndProperty();
                return;
            }

            // Draw label and get remaining rect
            Rect contentRect = EditorGUI.PrefixLabel(position, label);

            // Split content area into 3 equal parts
            float fieldWidth = (contentRect.width - ComponentSpacing * 2f) / 3f;
            Rect majorRect = new Rect(contentRect.x, contentRect.y, fieldWidth, contentRect.height);
            Rect minorRect = new Rect(majorRect.xMax + ComponentSpacing, contentRect.y, fieldWidth, contentRect.height);
            Rect patchRect = new Rect(minorRect.xMax + ComponentSpacing, contentRect.y, fieldWidth, contentRect.height);

            // Temporarily remove indent for inline fields
            int prevIndent = EditorGUI.indentLevel;
            EditorGUI.indentLevel = 0;

            EditorGUI.BeginChangeCheck();

            int major = EditorGUI.IntField(majorRect, majorProp.intValue);
            int minor = EditorGUI.IntField(minorRect, minorProp.intValue);
            int patch = EditorGUI.IntField(patchRect, patchProp.intValue);

            if (EditorGUI.EndChangeCheck())
            {
                // Clamp negative values to 0
                majorProp.intValue = major < 0 ? 0 : major;
                minorProp.intValue = minor < 0 ? 0 : minor;
                patchProp.intValue = patch < 0 ? 0 : patch;
            }

            EditorGUI.indentLevel = prevIndent;
            EditorGUI.EndProperty();
        }
    }
}
