// SSOT: skills/devian-unity/11-common-system/18-bundle-settings/SKILL.md

#if UNITY_EDITOR

using UnityEditor;
using UnityEngine;

namespace Devian
{
    /// <summary>
    /// Devian/Create Settings 메뉴.
    /// BundleSettings를 Resources/Devian 폴더에 생성/보수한다.
    /// </summary>
    public static class BundleSettingsMenu
    {
        // Resources 폴더 경로 (SSOT)
        private const string ResourcesFolderPath = "Assets/Resources/Devian";
        [MenuItem("Devian/Create Settings")]
        private static void CreateSettings()
        {
            // 1) Resources/Devian 폴더 보장
            EnsureFolder(ResourcesFolderPath);

            // 2) BundleSettings 생성/보수
            EnsureBundleSettings();

            Debug.Log("BundleSettings created/repaired successfully.");
        }

        /// <summary>
        /// 폴더를 보장한다 (없으면 생성).
        /// </summary>
        private static void EnsureFolder(string folderPath)
        {
            if (AssetDatabase.IsValidFolder(folderPath))
                return;

            // 상위 폴더부터 순차 생성
            var parts = folderPath.Split('/');
            var current = parts[0]; // "Assets"

            for (int i = 1; i < parts.Length; i++)
            {
                var next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, parts[i]);
                }
                current = next;
            }
        }

        /// <summary>
        /// BundleSettings 에셋을 생성하거나 보수한다.
        /// </summary>
        private static BundleSettings EnsureBundleSettings()
        {
            var existing = AssetDatabase.LoadAssetAtPath<BundleSettings>(BundleSettings.DefaultResourcesAssetPath);
            if (existing != null)
            {
                RepairSettings(existing);
                return existing;
            }

            // 없으면 새로 생성
            var asset = ScriptableObject.CreateInstance<BundleSettings>();
            RepairSettings(asset);

            AssetDatabase.CreateAsset(asset, BundleSettings.DefaultResourcesAssetPath);
            AssetDatabase.SaveAssets();

            Debug.Log($"BundleSettings created at: {BundleSettings.DefaultResourcesAssetPath}");
            return asset;
        }

        /// <summary>
        /// Settings 기본값 보장 (auto-repair).
        /// </summary>
        private static void RepairSettings(BundleSettings settings)
        {
            settings.EnsureEntry("COMMON_EFFECT_ID", "Assets/Bundles/CommonEffects");
            settings.EnsureEntry("MATERIAL_EFFECT_ID", "Assets/Bundles/MaterialEffects");

            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();
        }
    }
}

#endif
