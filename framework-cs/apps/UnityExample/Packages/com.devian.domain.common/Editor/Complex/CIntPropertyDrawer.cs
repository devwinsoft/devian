// SSOT: skills/devian-common/13-feature-complex/SKILL.md
// PropertyDrawer for CInt - displays unmasked value in Inspector

using UnityEditor;
using UnityEngine;

namespace Devian
{
    [CustomPropertyDrawer(typeof(CInt))]
    public class CIntPropertyDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);

            var save1Prop = property.FindPropertyRelative("save1");
            var save2Prop = property.FindPropertyRelative("save2");

            if (save1Prop == null || save2Prop == null)
            {
                EditorGUI.LabelField(position, label.text, "(CInt: missing fields)");
                EditorGUI.EndProperty();
                return;
            }

            // Get current unmasked value
            var temp = new CInt();
            temp.SetRaw(save1Prop.intValue, save2Prop.intValue);
            int currentValue = temp.GetValue();

            // Draw int field
            EditorGUI.BeginChangeCheck();
            int newValue = EditorGUI.IntField(position, label, currentValue);

            if (EditorGUI.EndChangeCheck())
            {
                // Set new value (generates new mask)
                temp.SetValue(newValue);
                save1Prop.intValue = temp.save1;
                save2Prop.intValue = temp.save2;
            }

            EditorGUI.EndProperty();
        }
    }
}
