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
    /// InputManager 커스텀 인스펙터.
    /// "Refresh Expected Button Keys" 버튼으로 Asset 내 Button 액션을 자동 수집한다.
    /// </summary>
    [CustomEditor(typeof(InputManager))]
    public sealed class InputManagerInspector : Editor
    {
        private const int MaxButtonCount = 64;

        private const string VirtualGamepadMovePath = "<VirtualGamepad>/move";
        private const string VirtualGamepadLookPath = "<VirtualGamepad>/look";

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            EditorGUILayout.Space(10);

            var assetProp = serializedObject.FindProperty("_asset");
            var asset = assetProp.objectReferenceValue as InputActionAsset;

            bool disabled = asset == null || Application.isPlaying;

            using (new EditorGUI.DisabledScope(disabled))
            {
                if (GUILayout.Button("Refresh Expected Button Keys"))
                {
                    _refreshExpectedButtonKeys(asset);
                }
            }

            // --- VirtualGamepad binding installer ---
            EditorGUILayout.Space(8);

            using (new EditorGUI.DisabledScope(disabled))
            {
                if (GUILayout.Button("Install/Ensure VirtualGamepad Bindings"))
                {
                    _installVirtualGamepadBindings(asset);
                }
            }

            if (asset == null)
            {
                EditorGUILayout.HelpBox("Assign an InputActionAsset to enable buttons.", MessageType.Info);
            }

            if (Application.isPlaying)
            {
                EditorGUILayout.HelpBox(
                    "Buttons are disabled in Play Mode. Stop play mode to edit.",
                    MessageType.Info);
            }
        }

        private void _refreshExpectedButtonKeys(InputActionAsset asset)
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
                    $"[InputManagerInspector] Expected Button Keys has {keys.Count} items. " +
                    $"Trimming to {MaxButtonCount}.");
                keys = keys.Take(MaxButtonCount).ToList();
            }

            // Update serialized property
            var prop = serializedObject.FindProperty("_expectedButtonKeys");
            prop.ClearArray();

            for (int i = 0; i < keys.Count; i++)
            {
                prop.InsertArrayElementAtIndex(i);
                prop.GetArrayElementAtIndex(i).stringValue = keys[i];
            }

            serializedObject.ApplyModifiedProperties();

            // Rebuild internal button map immediately so GetButtonIndex works right away
            var mgr = (InputManager)target;
            mgr.RebuildButtonMap();

            EditorUtility.SetDirty(mgr);

            Debug.Log($"[InputManagerInspector] Refreshed {keys.Count} button key(s).");
        }

        // ---- VirtualGamepad Binding Installer ----

        private void _installVirtualGamepadBindings(InputActionAsset asset)
        {
            Undo.RecordObject(asset, "Install VirtualGamepad Bindings");

            int added = 0;
            added += _ensureVirtualGamepadBinding(asset, "_moveKey", VirtualGamepadMovePath);
            added += _ensureVirtualGamepadBinding(asset, "_lookKey", VirtualGamepadLookPath);

            EditorUtility.SetDirty(asset);

            // Persist changes deterministically for .inputactions source assets.
            // Saving the imported InputActionAsset alone may not survive reimport/restart.
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
                ? $"[InputManagerInspector] VirtualGamepad bindings installed. Added {added} binding(s)."
                : "[InputManagerInspector] VirtualGamepad bindings already present. No changes.");
        }

        private int _ensureVirtualGamepadBinding(InputActionAsset asset, string propName, string bindingPath)
        {
            var keyProp = serializedObject.FindProperty(propName);
            string actionKey = keyProp.stringValue;

            if (!_tryResolveAction(asset, actionKey, out var action))
            {
                Debug.LogWarning($"[InputManagerInspector] Action not found: '{actionKey}' for '{bindingPath}'.");
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
