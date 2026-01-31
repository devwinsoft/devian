// SSOT: skills/devian-unity/30-unity-components/27-bootstrap-resource-object/SKILL.md

#if UNITY_EDITOR

using System.IO;
using UnityEditor;
using UnityEngine;

namespace Devian
{
    /// <summary>
    /// Devian/Create Bootstrap 메뉴.
    /// DevianSettings와 BootstrapRoot prefab을 Resources/Devian 폴더에 생성/보수한다.
    /// 기존 Assets/Settings 경로에 있는 DevianSettings는 자동 마이그레이션된다.
    /// </summary>
    public static class DevianSettingsMenu
    {
        // Resources 폴더 경로 (SSOT)
        private const string ResourcesFolderPath = "Assets/Resources/Devian";

        // BootstrapRoot prefab 경로 (Resources.Load 경로: "Devian/BootstrapRoot")
        private const string BootstrapRootPrefabPath = "Assets/Resources/Devian/BootstrapRoot.prefab";

        [MenuItem("Devian/Create Bootstrap")]
        private static void CreateBootstrap()
        {
            // 1) Resources/Devian 폴더 보장
            EnsureFolder(ResourcesFolderPath);

            // 2) DevianSettings 생성/보수 (마이그레이션 포함)
            var settings = EnsureDevianSettings();

            // 3) BootstrapRoot Prefab 생성/보수
            EnsureBootstrapRootPrefab(settings);

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
            settings.EnsureAssetId("EFFECT", "Assets/Bundles/Effects");
            settings.EnsurePlayerPrefsPrefix(DevianSettings.DefaultPlayerPrefsPrefix);
            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();
        }

        /// <summary>
        /// BootstrapRoot Prefab을 생성하거나 보수한다.
        /// </summary>
        private static void EnsureBootstrapRootPrefab(DevianSettings settings)
        {
            // 기존 prefab 로드 시도
            var existingPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(BootstrapRootPrefabPath);
            if (existingPrefab != null)
            {
                // 보수: 컴포넌트 확인 및 Settings 참조 갱신
                RepairBootstrapRootPrefab(existingPrefab, settings);
                return;
            }

            // 새 prefab 생성
            CreateBootstrapRootPrefab(settings);
        }

        private static void CreateBootstrapRootPrefab(DevianSettings settings)
        {
            // 임시 GameObject 생성
            var go = new GameObject("[Devian] BootstrapRoot");

            // 컴포넌트 추가
            var root = go.AddComponent<DevianBootstrapRoot>();
            root.SetSettings(settings);

            go.AddComponent<BootCoordinator>();
            go.AddComponent<SceneTransManager>();

            // Prefab 저장
            PrefabUtility.SaveAsPrefabAsset(go, BootstrapRootPrefabPath);

            // 임시 객체 삭제
            Object.DestroyImmediate(go);

            AssetDatabase.Refresh();

            // 생성된 prefab 선택
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(BootstrapRootPrefabPath);
            if (prefab != null)
            {
                Selection.activeObject = prefab;
                EditorGUIUtility.PingObject(prefab);
            }

            Debug.Log($"BootstrapRoot prefab created at: {BootstrapRootPrefabPath}");
        }

        private static void RepairBootstrapRootPrefab(GameObject prefab, DevianSettings settings)
        {
            // Prefab 내용물을 임시로 인스턴스화
            var instance = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
            if (instance == null) return;

            bool modified = false;

            // DevianBootstrapRoot 확인/추가
            var root = instance.GetComponent<DevianBootstrapRoot>();
            if (root == null)
            {
                root = instance.AddComponent<DevianBootstrapRoot>();
                modified = true;
            }

            // Settings 참조 갱신
            if (root.Settings != settings)
            {
                root.SetSettings(settings);
                modified = true;
            }

            // BootCoordinator 확인/추가
            if (instance.GetComponent<BootCoordinator>() == null)
            {
                instance.AddComponent<BootCoordinator>();
                modified = true;
            }

            // SceneTransManager 확인/추가
            if (instance.GetComponent<SceneTransManager>() == null)
            {
                instance.AddComponent<SceneTransManager>();
                modified = true;
            }

            if (modified)
            {
                // 변경사항 저장
                PrefabUtility.SaveAsPrefabAsset(instance, BootstrapRootPrefabPath);
            }

            // 인스턴스 삭제
            Object.DestroyImmediate(instance);

            // prefab 선택
            var savedPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(BootstrapRootPrefabPath);
            if (savedPrefab != null)
            {
                Selection.activeObject = savedPrefab;
                EditorGUIUtility.PingObject(savedPrefab);
            }
        }
    }
}

#endif
