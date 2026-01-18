using UnityEngine;
using UnityEditor;

namespace Devian.Core
{
    public abstract class BasePropertyDrawer : PropertyDrawer
    {
        public const float OneLineHeight = 18f;
        public const float OneLineNext = 20f;
        public const float w_label = 160f;
        protected const float w_PlayTime = 50f;
        protected const float w_aniSpeed = 30f;

        protected enum PropertyDrawType
        {
            NORMAL,
            SKIP_ROOTDRAW,      // ROOT를 그리지 않고 skip한다.
            SKIP_ROOTDRAW_ZEROLABEL,      // label이 none으로 넘어오면... ROOT를 SKIP한다.
        }
        protected PropertyDrawType DRAW_Type = PropertyDrawType.NORMAL;
        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            return OneLineNext;
        }

        private int indentLevBackup = -1;
        private int OneLineStartCount = 0;

        protected Rect StartOneLineScope(Rect position)
        {
            if (OneLineStartCount <= 0 && indentLevBackup < 0)
            {
                indentLevBackup = EditorGUI.indentLevel;
                EditorGUI.indentLevel = 0;

                position.x += 15 * indentLevBackup;
                position.width -= 16 * indentLevBackup;
                OneLineStartCount = 1;
            }
            else
            {
                OneLineStartCount++;
            }
            return position;
        }
        protected void EndOneLineScope()
        {
            if (OneLineStartCount > 0)
            {
                OneLineStartCount--;
                if (OneLineStartCount == 0)
                {
                    EditorGUI.indentLevel = indentLevBackup;
                    indentLevBackup = -1;
                }
            }
        }

        protected virtual string GetNotExpandDescAddString(SerializedProperty property)
        {
            return null;
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            if (position.width <= 1f) return;


            PropertyDrawType rootDrawType = DRAW_Type;

            if (rootDrawType == PropertyDrawType.SKIP_ROOTDRAW_ZEROLABEL)
            {
                if (label == GUIContent.none)
                {
                    rootDrawType = PropertyDrawType.SKIP_ROOTDRAW;
                }
                else
                {
                    rootDrawType = PropertyDrawType.NORMAL;
                }
            }

            switch (rootDrawType)
            {
                case PropertyDrawType.NORMAL:
                    {
                        Rect rcLine = position;
                        rcLine.height = OneLineHeight;

                        if (property.isExpanded)
                        {
                            EditorGUI.PropertyField(rcLine, property, label, false);

                            position.y += OneLineNext;
                            position.height -= OneLineNext;
                            EditorGUI.indentLevel++;
                            OnGUIDrawProperty(position, property, label);
                            EditorGUI.indentLevel--;
                        }
                        else
                        {
                            string noExpandDescAdd = GetNotExpandDescAddString(property);

                            if (string.IsNullOrEmpty(noExpandDescAdd))
                            {
                                EditorGUI.PropertyField(rcLine, property, label, false);
                            }
                            else
                            {
                                GUIContent label_desc = new GUIContent(label.text + noExpandDescAdd);
                                EditorGUI.PropertyField(rcLine, property, label_desc, false);
                            }
                        }
                    }
                    break;

                case PropertyDrawType.SKIP_ROOTDRAW:
                    OnGUIDrawProperty(position, property, label);
                    return;



            }
        }

        protected abstract void OnGUIDrawProperty(Rect position, SerializedProperty propertyRoot, GUIContent label);

        public static void OnGUI_FixedLabelPropertyField(Rect position, SerializedProperty property, float label_width, string strLabel)
        {
            Rect rcLabel = position;
            rcLabel.width = label_width;

            position.x += label_width + 1;
            position.width -= label_width + 1;

            EditorGUI.LabelField(rcLabel, new GUIContent(strLabel == null ? property.name : strLabel));
            EditorGUI.PropertyField(position, property, GUIContent.none);
        }
        public static void OnGUI_FixedLabelPropertyField(Rect position, SerializedProperty property, float label_width, GUIContent label = null)
        {
            Rect rcLabel = position;
            rcLabel.width = label_width;

            position.x += label_width + 1;
            position.width -= label_width + 1;

            EditorGUI.LabelField(rcLabel, label == null ? new GUIContent(property.name) : label);
            EditorGUI.PropertyField(position, property, GUIContent.none);
        }

        public static Rect DrawPropertyLabel(Rect positionToatal, SerializedProperty property, GUIContent label = null)
        {
            Rect labelRect = positionToatal;
            labelRect.width = w_label;

            if (label != null)
            {
                EditorGUI.LabelField(labelRect, label);
            }
            else
            {
                EditorGUI.LabelField(labelRect, property.name);
            }

            positionToatal.x = positionToatal.x + w_label;
            positionToatal.width = positionToatal.width - w_label;
            return positionToatal;
        }

        public bool DrawArrayExHeader(Rect rcOneLine, string name, string noEleamName, SerializedProperty prop)
        {
            if (prop == null) return false;

            rcOneLine = StartOneLineScope(rcOneLine);

            int arySize = prop.arraySize;

            if (arySize == 0)
            {
                Rect[] rcSplit = LineRectSplit2(rcOneLine, 0.7f, 10f);

                EditorGUI.PropertyField(rcSplit[0], prop, new GUIContent(noEleamName), false);
                if (GUI.Button(rcSplit[1], "Add"))
                {
                    prop.InsertArrayElementAtIndex(prop.arraySize);
                }
            }
            else
            {
                Rect[] rcSplit = LineRectSplit3(rcOneLine, 0.5f, 0.25f, 10f);

                string dispaly = name + "(" + prop.arraySize + ")";
                EditorGUI.PropertyField(rcSplit[0], prop, new GUIContent(dispaly), false);

                if (GUI.Button(rcSplit[1], "Add"))
                {
                    prop.InsertArrayElementAtIndex(prop.arraySize);
                }

                if (GUI.Button(rcSplit[2], "Del"))
                {
                    if (prop.arraySize > 0)
                        prop.DeleteArrayElementAtIndex(prop.arraySize - 1);
                }
            }

            EndOneLineScope();
            return prop.isExpanded;
        }

        protected Rect[] LineRectSplit2(Rect rcLine, float frontRatio, float inter_offset)
        {
            Rect[] ret = new Rect[2];
            ret[0] = rcLine;
            ret[1] = rcLine;

            ret[0].width = (float)(int)((rcLine.width - inter_offset) * frontRatio);

            ret[1].x = ret[0].xMax + inter_offset;
            ret[1].width = rcLine.xMax - ret[1].x;

            ret[0].y += 1;
            ret[1].y += 1;
            return ret;
        }

        protected Rect[] LineRectSplit3(Rect rcLine, float firstRatio, float lastRatio, float inter_offset)
        {
            Rect[] ret = new Rect[3];
            ret[0] = rcLine;
            ret[1] = rcLine;
            ret[2] = rcLine;

            float ctrl_tot_w = rcLine.width - inter_offset * 2;

            // 제일 앞부터 잘라낸다.
            ret[0].width = (float)(int)(ctrl_tot_w * firstRatio);

            // 제일 뒤 잘라낸다.
            ret[2].width = (float)(int)(ctrl_tot_w * lastRatio);
            ret[2].x = rcLine.xMax - ret[2].width;

            ret[1].x = ret[0].xMax + inter_offset;
            ret[1].width = ctrl_tot_w - ret[0].width - ret[2].width;


            ret[0].y += 1;
            ret[1].y += 1;
            ret[2].y += 1;
            return ret;
        }
    }
}
