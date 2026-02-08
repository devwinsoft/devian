#if UNITY_EDITOR

using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Devian
{
    /// <summary>
    /// Base selector for ScriptableObject-based AssetId (Editor-only).
    /// SearchDir source: Assets/Resources/Devian/DevianSettings.asset
    /// SSOT: skills/devian-unity/30-unity-components/29-render-effect-id/SKILL.md
    /// </summary>
    public abstract class BaseEditorScriptableAssetIdSelector<TAsset> : BaseEditorID_Selector
        where TAsset : ScriptableObject
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

            // ScriptableObject 스캔
            var guids = AssetDatabase.FindAssets($"t:{typeof(TAsset).Name}", new[] { searchDir });
            if (guids == null || guids.Length == 0)
            {
                return;
            }

            var normalizedSet = new HashSet<string>();
            for (var i = 0; i < guids.Length; i++)
            {
                var path = AssetDatabase.GUIDToAssetPath(guids[i]);
                var asset = AssetDatabase.LoadAssetAtPath<TAsset>(path);
                if (asset == null) continue;

                var name = asset.name ?? string.Empty;
                if (string.IsNullOrEmpty(name)) continue;

                // AssetManager policy: ignore @ prefix
                if (name.StartsWith("@"))
                {
                    continue;
                }

                var normalized = name.Trim().ToLowerInvariant();
                if (normalizedSet.Contains(normalized))
                {
                    Debug.LogError($"[AssetId] Duplicate ScriptableObject name (case-insensitive): '{name}'. Skipping.");
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
