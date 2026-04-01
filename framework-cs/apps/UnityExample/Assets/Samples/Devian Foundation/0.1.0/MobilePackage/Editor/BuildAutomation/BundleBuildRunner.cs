using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Build;
using UnityEditor.AddressableAssets.Build.DataBuilders;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;

namespace Devian
{
    /// <summary>
    /// Addressables 번들을 빌드한다.
    /// excludedGroupNames에 포함된 group은 빌드에서 제외한다.
    /// </summary>
    public static class BundleBuildRunner
    {
        /// <summary>
        /// Addressables 번들을 빌드한다.
        /// excludedGroupNames에 포함된 group은 빌드에서 제외한다.
        /// </summary>
        /// <param name="excludedGroupNames">제외할 group 이름 목록</param>
        /// <returns>성공 시 빌드 산출물 경로, 실패 시 null</returns>
        public static string Run(List<string> excludedGroupNames)
        {
            var aaSettings = AddressableAssetSettingsDefaultObject.Settings;
            if (aaSettings == null)
            {
                BuildAutomationLogger.LogError(
                    "[BundleBuild] AddressableAssetSettings not found. " +
                    "Addressables가 초기화되지 않았습니다.");
                return null;
            }

            // 프로필 검증 — activeProfileId가 없으면 NRE 발생
            if (!EnsureActiveProfile(aaSettings))
                return null;

            // null group 정리 — Addressables 내부에서 NRE 방지
            CleanNullGroups(aaSettings);

            // Addressables 내부 상태 pre-warm
            // BuildPlayerContent 전에 groups/schemas/entries/프로필 변수를 접근하여
            // 내부 캐시를 초기화한다. 이 단계를 생략하면 BuildPlayerContent에서 NRE 발생.
            WarmUpAddressables(aaSettings);

            // 제외할 group의 IncludeInBuild 원본 값을 백업
            var backup = new Dictionary<AddressableAssetGroup, bool>();

            try
            {
                // 제외 처리
                if (excludedGroupNames != null && excludedGroupNames.Count > 0)
                {
                    foreach (var group in aaSettings.groups)
                    {
                        if (group == null) continue;
                        if (!excludedGroupNames.Contains(group.Name)) continue;

                        var schema = group.GetSchema<BundledAssetGroupSchema>();
                        if (schema != null)
                        {
                            backup[group] = schema.IncludeInBuild;
                            schema.IncludeInBuild = false;
                            BuildAutomationLogger.Log(
                                $"[BundleBuild] Excluded group: {group.Name}");
                        }
                    }
                }

                // ActivePlayerDataBuilder 확인 및 설정
                EnsureActivePlayerDataBuilder(aaSettings);
                if (aaSettings.ActivePlayerDataBuilder == null)
                    return null;

                // 빌드 실행
                BuildAutomationLogger.Log(
                    $"[BundleBuild] BuildPlayerContent started... " +
                    $"(Builder: {aaSettings.ActivePlayerDataBuilder.Name})");

                AddressablesPlayerBuildResult result;
                try
                {
                    AddressableAssetSettings.BuildPlayerContent(out result);
                }
                catch (Exception ex)
                {
                    BuildAutomationLogger.LogError(
                        $"[BundleBuild] BuildPlayerContent threw exception: {ex.GetType().Name}");
                    BuildAutomationLogger.LogError(
                        $"[BundleBuild] Message: {ex.Message}");
                    BuildAutomationLogger.LogError(
                        $"[BundleBuild] StackTrace: {ex.StackTrace}");
                    return null;
                }

                if (result == null)
                {
                    BuildAutomationLogger.LogError(
                        "[BundleBuild] BuildPlayerContent returned null result.");
                    return null;
                }

                if (!string.IsNullOrEmpty(result.Error))
                {
                    BuildAutomationLogger.LogError(
                        $"[BundleBuild] Build failed: {result.Error}");
                    return null;
                }

                BuildAutomationLogger.Log(
                    $"[BundleBuild] Build succeeded. Output: {result.OutputPath}");
                return result.OutputPath;
            }
            finally
            {
                // 원본 IncludeInBuild 값 복원
                foreach (var kv in backup)
                {
                    var schema = kv.Key.GetSchema<BundledAssetGroupSchema>();
                    if (schema != null)
                        schema.IncludeInBuild = kv.Value;
                }

                if (backup.Count > 0)
                {
                    BuildAutomationLogger.Log(
                        $"[BundleBuild] Restored IncludeInBuild for {backup.Count} group(s)");
                }
            }
        }

        /// <summary>
        /// Addressables 내부 상태를 pre-warm한다.
        /// groups, schemas, entries, 프로필 변수 등을 미리 접근하여
        /// BuildPlayerContent 내부에서 lazy 초기화 시 NRE가 발생하는 것을 방지.
        /// </summary>
        private static void WarmUpAddressables(AddressableAssetSettings settings)
        {
            // 1) 전체 구조 접근 — groups, DataBuilders, ActiveBuilder
            var _ = settings.groups.Count;
            var __ = settings.DataBuilders.Count;
            var ___ = settings.ActivePlayerDataBuilderIndex;
            var builder = settings.ActivePlayerDataBuilder;
            if (builder != null) { var ____ = builder.GetType().Name; }

            // 2) 프로필 시스템 초기화
            var profileId = settings.activeProfileId;
            var profileNames = settings.profileSettings.GetAllProfileNames();

            // 3) 각 group의 schemas, entries, 프로필 변수 접근
            for (int i = 0; i < settings.groups.Count; i++)
            {
                var group = settings.groups[i];
                if (group == null) continue;

                var groupName = group.Name;
                var entryCount = group.entries != null ? group.entries.Count : 0;
                var bundleSchema = group.GetSchema<BundledAssetGroupSchema>();

                if (bundleSchema != null)
                {
                    var includeInBuild = bundleSchema.IncludeInBuild;

                    try
                    {
                        bundleSchema.BuildPath.GetValue(settings);
                        bundleSchema.LoadPath.GetValue(settings);
                    }
                    catch
                    {
                        // resolve 실패는 무시
                    }
                }
            }
        }

        /// <summary>
        /// groups 리스트에서 null 항목을 제거한다.
        /// Addressables 내부에서 null group 접근 시 NRE가 발생할 수 있다.
        /// </summary>
        private static void CleanNullGroups(AddressableAssetSettings settings)
        {
            var nullCount = 0;
            for (int i = settings.groups.Count - 1; i >= 0; i--)
            {
                if (settings.groups[i] == null)
                {
                    settings.groups.RemoveAt(i);
                    nullCount++;
                }
            }

            if (nullCount > 0)
            {
                BuildAutomationLogger.LogWarning(
                    $"[BundleBuild] Removed {nullCount} null group(s) from Addressables settings.");
            }
        }

        /// <summary>
        /// activeProfileId가 유효한지 확인하고, 없으면 기본 프로필을 설정한다.
        /// </summary>
        /// <returns>유효한 프로필이 있으면 true</returns>
        private static bool EnsureActiveProfile(AddressableAssetSettings settings)
        {
            var profileId = settings.activeProfileId;
            var profileSettings = settings.profileSettings;

            // 현재 profileId가 유효한지 확인
            if (!string.IsNullOrEmpty(profileId))
            {
                var profileName = profileSettings.GetProfileName(profileId);
                if (!string.IsNullOrEmpty(profileName))
                    return true;
            }

            // 유효하지 않으면 첫 번째 프로필 사용
            var allNames = profileSettings.GetAllProfileNames();
            if (allNames.Count == 0)
            {
                BuildAutomationLogger.LogError(
                    "[BundleBuild] No Addressables profiles found. " +
                    "Addressables Settings > Profiles에서 프로필을 생성하세요.");
                return false;
            }

            var firstId = profileSettings.GetProfileId(allNames[0]);
            settings.activeProfileId = firstId;
            BuildAutomationLogger.LogWarning(
                $"[BundleBuild] activeProfileId was invalid. " +
                $"Set to \"{allNames[0]}\" ({firstId})");
            return true;
        }

        /// <summary>
        /// ActivePlayerDataBuilder가 설정되어 있는지 확인하고,
        /// 미설정 시 BuildScriptPackedMode를 찾아 설정한다.
        /// </summary>
        private static void EnsureActivePlayerDataBuilder(AddressableAssetSettings settings)
        {
            if (settings.ActivePlayerDataBuilderIndex >= 0
                && settings.ActivePlayerDataBuilderIndex < settings.DataBuilders.Count
                && settings.ActivePlayerDataBuilder != null)
            {
                return; // 이미 설정됨
            }

            var packedIndex = -1;
            for (int i = 0; i < settings.DataBuilders.Count; i++)
            {
                if (settings.DataBuilders[i] is BuildScriptPackedMode)
                {
                    packedIndex = i;
                    break;
                }
            }

            if (packedIndex >= 0)
            {
                settings.ActivePlayerDataBuilderIndex = packedIndex;
                BuildAutomationLogger.Log(
                    "[BundleBuild] ActivePlayerDataBuilder was not set. " +
                    "Set to BuildScriptPackedMode.");
            }
            else
            {
                BuildAutomationLogger.LogError(
                    "[BundleBuild] ActivePlayerDataBuilder is not set and " +
                    "BuildScriptPackedMode not found in DataBuilders. " +
                    "Addressables Settings > Build and Play Mode Scripts를 확인하세요.");
            }
        }
    }
}
