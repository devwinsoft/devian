// SSOT: skills/devian-unity/30-unity-components/27-bootstrap-resource-object/SKILL.md

#if UNITY_EDITOR

using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Devian
{
    /// <summary>
    /// Devian/Create Bootstrap 메뉴.
    /// DevianSettings와 Bootstrap prefab을 Resources/Devian 폴더에 생성/보수한다.
    /// 기존 Assets/Settings 경로에 있는 DevianSettings는 자동 마이그레이션된다.
    ///
    /// Bootstrap prefab에는 BaseBootstrap 파생 컴포넌트가 정확히 1개 있어야 한다 (개발자 책임).
    /// 프레임워크가 유저 파생을 자동 추가하지 않는다.
    /// </summary>
    public static class DevianSettingsMenu
    {
        // Resources 폴더 경로 (SSOT)
        private const string ResourcesFolderPath = "Assets/Resources/Devian";

        // Bootstrap prefab 경로 (Resources.Load 경로: "Devian/Bootstrap")
        private const string BootstrapPrefabPath = "Assets/Resources/Devian/Bootstrap.prefab";

        [MenuItem("Devian/Create Bootstrap")]
        private static void CreateBootstrap()
        {
            // 1) Resources/Devian 폴더 보장
            EnsureFolder(ResourcesFolderPath);

            // 2) DevianSettings 생성/보수 (마이그레이션 포함)
            EnsureDevianSettings();

            // 3) Bootstrap Prefab 생성/보수
            EnsureBootstrapPrefab();

            Debug.Log("Devian Bootstrap created/repaired successfully.");
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

        /// <summary>
        /// Bootstrap Prefab을 생성하거나 보수한다.
        /// 프레임워크는 SceneTransManager만 추가한다.
        /// BaseBootstrap 파생 컴포넌트는 개발자가 직접 추가해야 한다.
        /// </summary>
        private static void EnsureBootstrapPrefab()
        {
            // 기존 prefab 로드 시도
            var existingPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(BootstrapPrefabPath);
            if (existingPrefab != null)
            {
                // 보수: 컴포넌트 확인
                RepairBootstrapPrefab(existingPrefab);
                return;
            }

            // 새 prefab 생성
            CreateBootstrapPrefab();
        }

        private static void CreateBootstrapPrefab()
        {
            // 임시 GameObject 생성
            var go = new GameObject("[Devian] Bootstrap");

            // 기본 컴포넌트 추가 (SceneTransManager만 - BaseBootstrap 파생은 개발자 책임)
            go.AddComponent<SceneTransManager>();

            // Prefab 저장
            PrefabUtility.SaveAsPrefabAsset(go, BootstrapPrefabPath);

            // 임시 객체 삭제
            UnityEngine.Object.DestroyImmediate(go);

            AssetDatabase.Refresh();

            // 생성된 prefab 선택
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(BootstrapPrefabPath);
            if (prefab != null)
            {
                Selection.activeObject = prefab;
                EditorGUIUtility.PingObject(prefab);
            }

            Debug.Log($"Bootstrap prefab created at: {BootstrapPrefabPath}");
            Debug.LogWarning("Bootstrap prefab requires a BaseBootstrap-derived component. Please add your custom bootstrap component.");
        }

        private static void RepairBootstrapPrefab(GameObject prefab)
        {
            // Prefab 내용물을 임시로 인스턴스화
            var instance = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
            if (instance == null) return;

            bool modified = false;

            // SceneTransManager 확인/추가
            if (instance.GetComponent<SceneTransManager>() == null)
            {
                instance.AddComponent<SceneTransManager>();
                modified = true;
            }

            if (modified)
            {
                // 변경사항 저장
                PrefabUtility.SaveAsPrefabAsset(instance, BootstrapPrefabPath);
            }

            // 인스턴스 삭제
            UnityEngine.Object.DestroyImmediate(instance);

            // prefab 선택
            var savedPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(BootstrapPrefabPath);
            if (savedPrefab != null)
            {
                Selection.activeObject = savedPrefab;
                EditorGUIUtility.PingObject(savedPrefab);
            }
        }
    }
}

#endif
