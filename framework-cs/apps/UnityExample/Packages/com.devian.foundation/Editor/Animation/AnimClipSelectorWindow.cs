using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Devian
{
    public sealed class AnimClipSelectorWindow : EditorWindow
    {
        private SerializedProperty _targetProperty;
        private AnimationClip[] _clips = Array.Empty<AnimationClip>();
        private string[] _names = Array.Empty<string>();
        private string _search = string.Empty;
        private Vector2 _scroll;

        public static AnimClipSelectorWindow Open(SerializedProperty targetProperty, RuntimeAnimatorController controller)
        {
            var w = CreateInstance<AnimClipSelectorWindow>();
            w.titleContent = new GUIContent("Select Animation Clip");
            w._Bind(targetProperty, controller);
            w.ShowUtility();
            return w;
        }

        private void _Bind(SerializedProperty targetProperty, RuntimeAnimatorController controller)
        {
            _targetProperty = targetProperty;

            if (controller != null)
            {
                // controller.animationClips는 중복 clip이 나올 수 있어 distinct 처리
                _clips = controller.animationClips.Where(c => c != null).Distinct().ToArray();
                _names = _clips.Select(c => c.name).ToArray();
            }
            else
            {
                _clips = Array.Empty<AnimationClip>();
                _names = Array.Empty<string>();
            }
        }

        private void OnGUI()
        {
            if (_targetProperty == null)
            {
                Close();
                return;
            }

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Search", GUILayout.Width(50));
            _search = EditorGUILayout.TextField(_search);
            EditorGUILayout.EndHorizontal();

            var filtered = string.IsNullOrEmpty(_search)
                ? Enumerable.Range(0, _clips.Length).ToArray()
                : Enumerable.Range(0, _clips.Length)
                    .Where(i => _names[i].IndexOf(_search, StringComparison.OrdinalIgnoreCase) >= 0)
                    .ToArray();

            _scroll = EditorGUILayout.BeginScrollView(_scroll);

            foreach (var i in filtered)
            {
                if (GUILayout.Button(_names[i], GUILayout.Height(22)))
                {
                    _targetProperty.objectReferenceValue = _clips[i];
                    _targetProperty.serializedObject.ApplyModifiedProperties();
                    Close();
                    GUIUtility.ExitGUI();
                }
            }

            EditorGUILayout.EndScrollView();
        }
    }
}
