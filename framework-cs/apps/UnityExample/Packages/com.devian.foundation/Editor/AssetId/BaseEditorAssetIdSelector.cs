#if UNITY_EDITOR

using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Devian
{
    /// <summary>
    /// Base selector for AssetId (Editor-only).
    /// SearchDir source: Assets/Resources/Devian/DevianSettings.asset
    /// SSOT: skills/devian-unity/10-base-system/21-asset-id/SKILL.md
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

            var searchDir = ResolveSearchDirOrFallback(GroupKey);
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

        private static string ResolveSearchDirOrFallback(string groupKey)
        {
            var settings = AssetDatabase.LoadAssetAtPath<DevianSettings>(DevianSettings.DefaultResourcesAssetPath);
            if (settings == null)
            {
                Debug.LogWarning($"[AssetId] DevianSettings not found at '{DevianSettings.DefaultResourcesAssetPath}'. Using fallback searchDir: Assets");
                return "Assets";
            }

            var dir = settings.GetAssetIdSearchDir(groupKey);
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
