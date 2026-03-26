using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Build;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;

namespace Devian
{
    public static class BundleBuildRunner
    {
        public static DateTime LastBuildStartTime { get; private set; }

        public static string Run(
            List<string> excludedGroupNames,
            BuildAutomationSettings buildSettings = null)
        {
            LastBuildStartTime = DateTime.UtcNow;

            var aaSettings = AddressableAssetSettingsDefaultObject.Settings;
            if (aaSettings == null)
            {
                BuildAutomationLogger.LogError("[BundleBuild] AddressableAssetSettings not found.");
                return null;
            }

            // Remote.LoadPath 자동 동기화
            if (buildSettings != null && !string.IsNullOrEmpty(buildSettings.remoteCdnUrl))
            {
                SyncRemoteLoadPath(aaSettings, buildSettings.remoteCdnUrl);
            }

            // 제외 그룹 백업 & 비활성화
            var backup = new Dictionary<AddressableAssetGroup, bool>();
            if (excludedGroupNames != null)
            {
                foreach (var g in aaSettings.groups)
                {
                    if (g == null) continue;
                    if (!excludedGroupNames.Contains(g.Name)) continue;
                    var schema = g.GetSchema<BundledAssetGroupSchema>();
                    if (schema != null)
                    {
                        backup[g] = schema.IncludeInBuild;
                        schema.IncludeInBuild = false;
                        BuildAutomationLogger.Log($"[BundleBuild] Excluded group: {g.Name}");
                    }
                }
            }

            try
            {
                BuildAutomationLogger.Log(
                    $"[BundleBuild] BuildPlayerContent started... (Builder: {aaSettings.ActivePlayerDataBuilder?.Name ?? "NULL"})");

                AddressablesPlayerBuildResult result;
                AddressableAssetSettings.BuildPlayerContent(out result);

                if (result == null)
                {
                    BuildAutomationLogger.LogError("[BundleBuild] BuildPlayerContent returned null.");
                    return null;
                }

                if (!string.IsNullOrEmpty(result.Error))
                {
                    BuildAutomationLogger.LogError($"[BundleBuild] Build failed: {result.Error}");
                    return null;
                }

                BuildAutomationLogger.Log($"[BundleBuild] Build succeeded. Output: {result.OutputPath}");
                return result.OutputPath;
            }
            catch (Exception ex)
            {
                BuildAutomationLogger.LogError($"[BundleBuild] Exception: {ex}");
                return null;
            }
            finally
            {
                foreach (var kv in backup)
                {
                    var schema = kv.Key.GetSchema<BundledAssetGroupSchema>();
                    if (schema != null)
                        schema.IncludeInBuild = kv.Value;
                }
                if (backup.Count > 0)
                    BuildAutomationLogger.Log($"[BundleBuild] Restored IncludeInBuild for {backup.Count} group(s)");
            }
        }

        /// <summary>
        /// Addressables Profile의 Remote.LoadPath를
        /// {remoteCdnUrl}/v{bundleVersion}/[BuildTarget] 형태로 동기화한다.
        /// </summary>
        private static void SyncRemoteLoadPath(
            AddressableAssetSettings aaSettings, string remoteCdnUrl)
        {
            var cdnBase = remoteCdnUrl.TrimEnd('/');
            var expected = $"{cdnBase}/v[UnityEditor.PlayerSettings.bundleVersion]/[BuildTarget]";

            var profileId = aaSettings.activeProfileId;
            var current = aaSettings.profileSettings.GetValueByName(profileId, "Remote.LoadPath");

            if (current == expected) return;

            aaSettings.profileSettings.SetValue(profileId, "Remote.LoadPath", expected);
            EditorUtility.SetDirty(aaSettings);
            BuildAutomationLogger.Log(
                $"[BundleBuild] Remote.LoadPath synced → {expected}");
        }
    }
}
