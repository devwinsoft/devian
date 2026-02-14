// SSOT: skills/devian-unity/10-base-system/23-devian-settings/SKILL.md

#if UNITY_EDITOR

using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Devian
{
    /// <summary>
    /// Devian/Create Settings 메뉴.
    /// DevianSettings를 Resources/Devian 폴더에 생성/보수한다.
    /// 기존 Assets/Settings 경로에 있는 DevianSettings는 자동 마이그레이션된다.
    /// </summary>
    public static class DevianSettingsMenu
    {
        // Resources 폴더 경로 (SSOT)
        private const string ResourcesFolderPath = "Assets/Resources/Devian";

        [MenuItem("Devian/Create Settings")]
        private static void CreateSettings()
        {
            // 1) Resources/Devian 폴더 보장
            EnsureFolder(ResourcesFolderPath);

            // 2) DevianSettings 생성/보수 (마이그레이션 포함)
            EnsureDevianSettings();

            Debug.Log("DevianSettings created/repaired successfully.");
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
        /// DevianSettings 에셋을 생성하거나 보수한다.
        /// 레거시 경로(Assets/Settings)에 있으면 Resources로 마이그레이션한다.
        /// </summary>
        private static DevianSettings EnsureDevianSettings()
        {
            // 1) 정본 경로(Resources)에서 먼저 확인
            var existing = AssetDatabase.LoadAssetAtPath<DevianSettings>(DevianSettings.DefaultResourcesAssetPath);
            if (existing != null)
            {
                RepairSettings(existing);
                return existing;
            }

            // 2) 레거시 경로 확인 및 마이그레이션
            var legacy = AssetDatabase.LoadAssetAtPath<DevianSettings>(DevianSettings.LegacyAssetPath);
            if (legacy != null)
            {
                // 마이그레이션: 레거시 → Resources
                var moveResult = AssetDatabase.MoveAsset(DevianSettings.LegacyAssetPath, DevianSettings.DefaultResourcesAssetPath);
                if (string.IsNullOrEmpty(moveResult))
                {
                    Debug.Log($"DevianSettings migrated: {DevianSettings.LegacyAssetPath} → {DevianSettings.DefaultResourcesAssetPath}");

                    // 이동된 에셋 다시 로드
                    var migrated = AssetDatabase.LoadAssetAtPath<DevianSettings>(DevianSettings.DefaultResourcesAssetPath);
                    if (migrated != null)
                    {
                        RepairSettings(migrated);
                        return migrated;
                    }
                }
                else
                {
                    Debug.LogWarning($"Failed to migrate DevianSettings: {moveResult}");
                    // 마이그레이션 실패 시 레거시 에셋 보수 후 반환
                    RepairSettings(legacy);
                    return legacy;
                }
            }

            // 3) 둘 다 없으면 새로 생성
            var asset = ScriptableObject.CreateInstance<DevianSettings>();
            RepairSettings(asset);

            AssetDatabase.CreateAsset(asset, DevianSettings.DefaultResourcesAssetPath);
            AssetDatabase.SaveAssets();

            Debug.Log($"DevianSettings created at: {DevianSettings.DefaultResourcesAssetPath}");
            return asset;
        }

        /// <summary>
        /// Settings 기본값 보장 (auto-repair).
        /// </summary>
        private static void RepairSettings(DevianSettings settings)
        {
            settings.EnsureAssetId("COMMON_EFFECT", "Assets/Bundles/Effects");
            settings.EnsureAssetId("MATERIAL_EFFECT", "Assets/Bundles/MaterialEffects");

            settings.EnsurePlayerPrefsPrefix(DevianSettings.DefaultPlayerPrefsPrefix);
            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();
        }
    }
}

#endif
