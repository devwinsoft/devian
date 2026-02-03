using UnityEditor;
using UnityEngine;

namespace Devian
{
    [CustomPropertyDrawer(typeof(AnimSequenceStep))]
    public sealed class AnimSequenceStep_Drawer : PropertyDrawer
    {
        private const float Line = 18f;
        private const float Pad = 2f;

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            // Clip, Speed, Repeat, FadeTime = 4줄
            return (Line + Pad) * 4f;
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);

            var clipProp = property.FindPropertyRelative("Clip");
            var speedProp = property.FindPropertyRelative("Speed");
            var repeatProp = property.FindPropertyRelative("Repeat");
            var fadeProp = property.FindPropertyRelative("FadeTime");

            var r = position;
            r.height = Line;

            // Clip row: ObjectField + Select 버튼
            DrawClipRow(r, property, clipProp);

            r.y += Line + Pad;
            EditorGUI.PropertyField(r, speedProp);

            r.y += Line + Pad;
            EditorGUI.PropertyField(r, repeatProp);

            r.y += Line + Pad;
            EditorGUI.PropertyField(r, fadeProp);

            EditorGUI.EndProperty();
        }

        private static void DrawClipRow(Rect r, SerializedProperty stepProp, SerializedProperty clipProp)
        {
            var btnW = 60f;
            var fieldR = new Rect(r.x, r.y, r.width - btnW - 6f, r.height);
            var btnR = new Rect(fieldR.xMax + 6f, r.y, btnW, r.height);

            EditorGUI.ObjectField(fieldR, clipProp, GUIContent.none);

            using (new EditorGUI.DisabledScope(!TryFindController(stepProp, out var controller)))
            {
                if (GUI.Button(btnR, "Select"))
                {
                    AnimClipSelectorWindow.Open(clipProp, controller);
                }
            }
        }

        private static bool TryFindController(SerializedProperty stepProp, out RuntimeAnimatorController controller)
        {
            controller = null;

            var so = stepProp.serializedObject;
            if (so == null) return false;

            // targetObject가 Component/ScriptableObject일 수 있으나, 여기선 Component 케이스만 지원
            var target = so.targetObject as Component;
            if (target == null) return false;

            // 우선순위: 자식 Animator -> 부모 Animator
            var animator = target.GetComponentInChildren<Animator>();
            if (animator == null)
            {
                animator = target.GetComponentInParent<Animator>();
            }

            if (animator == null) return false;

            controller = animator.runtimeAnimatorController;
            return controller != null;
        }
    }
}
