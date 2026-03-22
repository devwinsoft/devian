#if UNITY_EDITOR

using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Devian
{
    /// <summary>
    /// Base selector for AssetId (Editor-only).
    /// Default SearchDir source: Assets/Resources/Devian/BundleSettings.asset
    /// 서브클래스가 ResolveSearchDir()를 override하여 다른 settings source를 사용할 수 있다.
    /// SSOT: skills/devian-unity/20-common-package/12-asset-id/SKILL.md
    /// </summary>
    public abstract class BaseEditorAssetIdSelector<TComponent> : BaseEditorID_Selector
        where TComponent : Component
    {
        protected abstract string GroupKey { get; }
        protected abstract string DisplayTypeName { get; }

        protected override string GetDisplayTypeName()
        {
            return DisplayTypeName;
        }

        public override void Reload()
        {
            ClearItems();

            var searchDir = ResolveSearchDir(GroupKey);
            var prefabs = AssetManager.FindPrefabs<TComponent>(new[] { searchDir });

            if (prefabs == null || prefabs.Length == 0)
            {
                return;
            }

            var normalizedSet = new HashSet<string>();
            for (var i = 0; i < prefabs.Length; i++)
            {
                var prefab = prefabs[i];
                if (prefab == null) continue;

                var name = prefab.name ?? string.Empty;
                if (string.IsNullOrEmpty(name)) continue;

                // AssetManager policy: ignore @ prefabs
                if (name.StartsWith("@"))
                {
                    continue;
                }

                var normalized = name.Trim().ToLowerInvariant();
                if (normalizedSet.Contains(normalized))
                {
                    Debug.LogError($"[AssetId] Duplicate prefab name (case-insensitive): '{name}'. Skipping.");
                    continue;
                }
                normalizedSet.Add(normalized);

                AddItem(name, name);
            }
        }

        /// <summary>
        /// GroupKey로 SearchDir을 해석한다.
        /// 기본 구현은 BundleSettings에서 조회한다.
        /// 서브클래스가 override하여 다른 settings source를 사용할 수 있다.
        /// </summary>
        protected virtual string ResolveSearchDir(string groupKey)
        {
            var settings = AssetDatabase.LoadAssetAtPath<BundleSettings>(BundleSettings.DefaultResourcesAssetPath);
            if (settings == null)
            {
                Debug.LogWarning($"[AssetId] BundleSettings not found at '{BundleSettings.DefaultResourcesAssetPath}'. Using fallback searchDir: Assets");
                return "Assets";
            }

            var dir = settings.GetEntry(groupKey);
            if (string.IsNullOrWhiteSpace(dir))
            {
                return "Assets";
            }

            if (!AssetDatabase.IsValidFolder(dir))
            {
                Debug.LogWarning($"[AssetId] SearchDir '{dir}' does not exist. Using fallback: Assets");
                return "Assets";
            }

            return dir;
        }
    }
}

#endif
