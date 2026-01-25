// SSOT: skills/devian-common-feature/13-feature-complex/SKILL.md
// PropertyDrawer for CString - displays unmasked value in Inspector

using System;
using UnityEditor;
using UnityEngine;

namespace Devian
{
    [CustomPropertyDrawer(typeof(CString))]
    public class CStringPropertyDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);

            var dataProp = property.FindPropertyRelative("data");

            if (dataProp == null)
            {
                EditorGUI.LabelField(position, label.text, "(CString: missing data field)");
                EditorGUI.EndProperty();
                return;
            }

            // Get current unmasked value
            string currentValue = string.Empty;
            bool decodeError = false;

            if (!string.IsNullOrEmpty(dataProp.stringValue))
            {
                try
                {
                    currentValue = ComplexUtil.Decrypt_Base64(dataProp.stringValue);
                }
                catch (Exception)
                {
                    decodeError = true;
                    currentValue = string.Empty;
                }
            }

            // Draw text field
            EditorGUI.BeginChangeCheck();
            string newValue = EditorGUI.TextField(position, label, currentValue);

            if (EditorGUI.EndChangeCheck())
            {
                // Set new value (generates new masked data)
                if (string.IsNullOrEmpty(newValue))
                {
                    dataProp.stringValue = string.Empty;
                }
                else
                {
                    dataProp.stringValue = ComplexUtil.Encrypt_Base64(newValue);
                }
            }

            // Show warning if decode failed
            if (decodeError)
            {
                var helpBoxRect = new Rect(position.x, position.y + EditorGUIUtility.singleLineHeight + 2, position.width, EditorGUIUtility.singleLineHeight);
                EditorGUI.HelpBox(helpBoxRect, "Decode failed - data may be corrupted", MessageType.Warning);
            }

            EditorGUI.EndProperty();
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            var dataProp = property.FindPropertyRelative("data");
            bool hasError = false;

            if (dataProp != null && !string.IsNullOrEmpty(dataProp.stringValue))
            {
                try
                {
                    ComplexUtil.Decrypt_Base64(dataProp.stringValue);
                }
                catch
                {
                    hasError = true;
                }
            }

            return hasError 
                ? EditorGUIUtility.singleLineHeight * 2 + 4 
                : EditorGUIUtility.singleLineHeight;
        }
    }
}
