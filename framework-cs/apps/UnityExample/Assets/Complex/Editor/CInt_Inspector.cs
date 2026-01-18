using UnityEngine;
using UnityEditor;

namespace Devian.Core
{
    [CustomPropertyDrawer(typeof(CInt))]
    public class CInt_Inspector : BasePropertyDrawer
    {
        public CInt_Inspector()
        {
            DRAW_Type = PropertyDrawType.SKIP_ROOTDRAW;
        }

        CInt script = new CInt();

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            return OneLineHeight;
        }

        protected override void OnGUIDrawProperty(Rect position, SerializedProperty property, GUIContent label)
        {
            SerializedProperty propData1 = property.FindPropertyRelative("save1");
            SerializedProperty propData2 = property.FindPropertyRelative("save2");

            script.LoadData(propData1.intValue, propData2.intValue);

            int prevValue = script.GetValue();
            int nextValue = EditorGUI.IntField(position, label, prevValue);
            if (prevValue != nextValue)
            {
                script = nextValue;
                propData1.intValue = script.save1;
                propData2.intValue = script.save2;
            }
        }
    }
}

