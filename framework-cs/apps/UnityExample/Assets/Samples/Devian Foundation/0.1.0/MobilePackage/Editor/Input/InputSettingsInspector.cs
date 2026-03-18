using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Devian
{
    /// <summary>
    /// InputSettings 커스텀 인스펙터.
    /// "Refresh Expected Button Keys" — Asset 내 Button 액션을 자동 수집하여 _expectedButtonKeys에 저장.
    /// "Install/Ensure VirtualGamepad Bindings" — Move/Look Action에 VirtualGamepad 바인딩을 보장.
    /// 내부 버튼 맵 재빌드는 InputManager.OnEnable() 초기화 시 자동 수행된다.
    /// </summary>
    [CustomEditor(typeof(InputSettings))]
    public sealed class InputSettingsInspector : Editor
    {
        private const int MaxButtonCount = 64;

        private const string VirtualGamepadMovePath = "<VirtualGamepad>/move";
        private const string VirtualGamepadLookPath = "<VirtualGamepad>/look";

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            EditorGUILayout.Space(10);

            var settings = (InputSettings)target;
            var asset = settings.Asset;

            bool disabled = asset == null || Application.isPlaying;

            using (new EditorGUI.DisabledScope(disabled))
            {
                if (GUILayout.Button("Refresh Expected Button Keys"))
                {
                    _refreshExpectedButtonKeys(settings, asset);
                }
            }

            // --- VirtualGamepad binding installer ---
            EditorGUILayout.Space(8);

            using (new EditorGUI.DisabledScope(disabled))
            {
                if (GUILayout.Button("Install/Ensure VirtualGamepad Bindings"))
                {
                    _installVirtualGamepadBindings(settings, asset);
                }
            }

            if (asset == null && !Application.isPlaying)
            {
                EditorGUILayout.HelpBox("Asset (InputActionAsset) is not assigned.", MessageType.Info);
            }

            if (Application.isPlaying)
            {
                EditorGUILayout.HelpBox(
                    "Buttons are disabled in Play Mode. Stop play mode to edit.",
                    MessageType.Info);
            }
        }

        private void _refreshExpectedButtonKeys(InputSettings settings, InputActionAsset asset)
        {
            var keys = new List<string>();

            foreach (var map in asset.actionMaps)
            {
                foreach (var action in map.actions)
                {
                    if (!string.Equals(action.expectedControlType, "Button", StringComparison.OrdinalIgnoreCase))
                        continue;

                    keys.Add($"{map.name}/{action.name}");
                }
            }

            // Deduplicate + Ordinal sort
            keys = keys
                .Distinct(StringComparer.Ordinal)
                .OrderBy(x => x, StringComparer.Ordinal)
                .ToList();

            if (keys.Count > MaxButtonCount)
            {
                Debug.LogWarning(
                    $"[InputSettingsInspector] Expected Button Keys has {keys.Count} items. " +
                    $"Trimming to {MaxButtonCount}.");
                keys = keys.Take(MaxButtonCount).ToList();
            }

            // Update serialized property directly (this IS the InputSettings serializedObject)
            serializedObject.Update();
            var prop = serializedObject.FindProperty("_expectedButtonKeys");
            prop.ClearArray();

            for (int i = 0; i < keys.Count; i++)
            {
                prop.InsertArrayElementAtIndex(i);
                prop.GetArrayElementAtIndex(i).stringValue = keys[i];
            }

            serializedObject.ApplyModifiedProperties();

            EditorUtility.SetDirty(settings);

            Debug.Log($"[InputSettingsInspector] Refreshed {keys.Count} button key(s).");
        }

        // ---- VirtualGamepad Binding Installer ----

        private void _installVirtualGamepadBindings(InputSettings settings, InputActionAsset asset)
        {
            Undo.RecordObject(asset, "Install VirtualGamepad Bindings");

            int added = 0;
            added += _ensureVirtualGamepadBinding(asset, settings.MoveKey, VirtualGamepadMovePath);
            added += _ensureVirtualGamepadBinding(asset, settings.LookKey, VirtualGamepadLookPath);

            EditorUtility.SetDirty(asset);

            // Persist changes deterministically for .inputactions source assets.
            var assetPath = AssetDatabase.GetAssetPath(asset);
            if (!string.IsNullOrEmpty(assetPath) &&
                assetPath.EndsWith(".inputactions", StringComparison.OrdinalIgnoreCase))
            {
                File.WriteAllText(assetPath, asset.ToJson());
                AssetDatabase.ImportAsset(assetPath);
            }
            else
            {
                AssetDatabase.SaveAssets();
            }

            Debug.Log(added > 0
                ? $"[InputSettingsInspector] VirtualGamepad bindings installed. Added {added} binding(s)."
                : "[InputSettingsInspector] VirtualGamepad bindings already present. No changes.");
        }

        private int _ensureVirtualGamepadBinding(InputActionAsset asset, string actionKey, string bindingPath)
        {
            if (!_tryResolveAction(asset, actionKey, out var action))
            {
                Debug.LogWarning($"[InputSettingsInspector] Action not found: '{actionKey}' for '{bindingPath}'.");
                return 0;
            }

            if (_hasBinding(action, bindingPath))
                return 0;

            action.AddBinding(bindingPath);
            return 1;
        }

        private static bool _tryResolveAction(InputActionAsset asset, string actionKey, out InputAction action)
        {
            action = null;
            if (string.IsNullOrEmpty(actionKey)) return false;

            int slash = actionKey.IndexOf('/');
            if (slash <= 0 || slash >= actionKey.Length - 1) return false;

            string mapName = actionKey.Substring(0, slash);
            string actionName = actionKey.Substring(slash + 1);

            var map = asset.FindActionMap(mapName, false);
            if (map == null) return false;

            action = map.FindAction(actionName, false);
            return action != null;
        }

        private static bool _hasBinding(InputAction action, string bindingPath)
        {
            foreach (var b in action.bindings)
            {
                if (string.Equals(b.path, bindingPath, StringComparison.Ordinal))
                    return true;

                if (!string.IsNullOrEmpty(b.effectivePath) &&
                    string.Equals(b.effectivePath, bindingPath, StringComparison.Ordinal))
                    return true;
            }
            return false;
        }
    }
}
