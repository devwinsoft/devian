#if UNITY_EDITOR

using System.IO;
using UnityEditor;
using UnityEngine;

namespace Devian
{
    public static class DevianSettingsMenu
    {
        [MenuItem("Devian/Settings/Create DevianSettings")]
        private static void CreateOrSelect()
        {
            var existing = AssetDatabase.LoadAssetAtPath<DevianSettings>(DevianSettings.DefaultAssetPath);
            if (existing != null)
            {
                // Auto-repair: ensure default entries exist
                existing.EnsureAssetId("EFFECT", "Assets/Bundles/Effects");
                existing.EnsurePlayerPrefsPrefix(DevianSettings.DefaultPlayerPrefsPrefix);
                EditorUtility.SetDirty(existing);
                AssetDatabase.SaveAssets();

                Selection.activeObject = existing;
                EditorGUIUtility.PingObject(existing);
                return;
            }

            var dir = Path.GetDirectoryName(DevianSettings.DefaultAssetPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            var asset = ScriptableObject.CreateInstance<DevianSettings>();

            // Default seed requested by user:
            // "assetId": { "EFFECT": "Assets/Bundles/Effects" }
            asset.EnsureAssetId("EFFECT", "Assets/Bundles/Effects");
            asset.EnsurePlayerPrefsPrefix(DevianSettings.DefaultPlayerPrefsPrefix);

            AssetDatabase.CreateAsset(asset, DevianSettings.DefaultAssetPath);
            AssetDatabase.SaveAssets();

            Selection.activeObject = asset;
            EditorGUIUtility.PingObject(asset);
        }
    }
}

#endif
