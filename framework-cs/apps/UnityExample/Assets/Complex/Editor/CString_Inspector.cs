using UnityEngine;
using UnityEditor;

namespace Devian.Core
{
    [CustomPropertyDrawer(typeof(CString))]
    public class CString_Inspector : BasePropertyDrawer
    {
        public CString_Inspector()
        {
            DRAW_Type = PropertyDrawType.SKIP_ROOTDRAW;
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            return OneLineHeight;
        }

        protected override void OnGUIDrawProperty(Rect position, SerializedProperty property, GUIContent label)
        {
            SerializedProperty propData = property.FindPropertyRelative("data");
            string prevValue = ComplexUtil.Decrypt_Base64(propData.stringValue);
            string nextValue = EditorGUI.TextField(position, label.text, prevValue);
            if (string.Equals(prevValue, nextValue) == false)
            {
                propData.stringValue = ComplexUtil.Encrypt_Base64(nextValue);
            }
        }
    }
}
