#if UNITY_EDITOR

using UnityEditor;
using UnityEngine;

namespace Devian
{
    [CustomPropertyDrawer(typeof(UIPopupFrameMapEntry))]
    public sealed class UIPopupFrameMapEntry_Drawer : PropertyDrawer
    {
        private const float ButtonWidth = 90f;
        private const float AutoFillButtonWidth = 80f;
        private const float VerticalSpacing = 2f;

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            return (EditorGUIUtility.singleLineHeight * 2f) + VerticalSpacing;
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);

            var frameTypeNameProp = property.FindPropertyRelative(nameof(UIPopupFrameMapEntry.FrameTypeName));
            var frameIdProp = property.FindPropertyRelative(nameof(UIPopupFrameMapEntry.FrameId));

            if (frameTypeNameProp == null || frameIdProp == null)
            {
                EditorGUI.LabelField(position, label.text, "Invalid UIPopupFrameMapEntry");
                EditorGUI.EndProperty();
                return;
            }

            var firstLine = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);
            var secondLine = new Rect(
                position.x,
                position.y + EditorGUIUtility.singleLineHeight + VerticalSpacing,
                position.width,
                EditorGUIUtility.singleLineHeight);

            var frameTypeLabelRect = new Rect(firstLine.x, firstLine.y, EditorGUIUtility.labelWidth, firstLine.height);
            var frameTypeValueRect = new Rect(
                frameTypeLabelRect.xMax,
                firstLine.y,
                firstLine.width - EditorGUIUtility.labelWidth - ButtonWidth - 2f,
                firstLine.height);
            var frameTypeButtonRect = new Rect(
                frameTypeValueRect.xMax + 2f,
                firstLine.y,
                ButtonWidth,
                firstLine.height);

            EditorGUI.LabelField(frameTypeLabelRect, "Frame Type");
            EditorGUI.SelectableLabel(frameTypeValueRect, GetDisplayName(frameTypeNameProp.stringValue), EditorStyles.textField);

            if (GUI.Button(frameTypeButtonRect, "Select Type"))
            {
                var window = ScriptableObject.CreateInstance<UIPopupFrameTypeSelector>();
                window.ShowUtility();
                window.Bind(frameTypeNameProp);
                window.Reload();
                window.Init(frameTypeNameProp.stringValue);
            }

            var frameIdFieldRect = new Rect(
                secondLine.x,
                secondLine.y,
                secondLine.width - AutoFillButtonWidth - 2f,
                secondLine.height);
            var autoFillButtonRect = new Rect(
                frameIdFieldRect.xMax + 2f,
                secondLine.y,
                AutoFillButtonWidth,
                secondLine.height);

            EditorGUI.PropertyField(frameIdFieldRect, frameIdProp, new GUIContent("Frame Id"), true);

            if (GUI.Button(autoFillButtonRect, "Auto Fill"))
            {
                if (!UIPopupFrameEditorUtility.TryAutoFillEntry(property, out var message) && !string.IsNullOrWhiteSpace(message))
                {
                    Debug.LogWarning(message);
                }
                else if (!string.IsNullOrWhiteSpace(message))
                {
                    Debug.Log(message);
                }
            }

            EditorGUI.EndProperty();
        }

        private static string GetDisplayName(string typeName)
        {
            if (!UIPopupFrameEditorUtility.TryResolvePopupFrameType(typeName, out var type))
            {
                return string.IsNullOrWhiteSpace(typeName) ? string.Empty : typeName;
            }

            return type.FullName ?? type.Name;
        }
    }
}

#endif
