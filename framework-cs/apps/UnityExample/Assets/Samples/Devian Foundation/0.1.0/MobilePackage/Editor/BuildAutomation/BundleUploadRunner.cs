using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEngine;

namespace Devian
{
    /// <summary>
    /// Remote group의 빌드 산출물을 release repo에 복사하고 git commit한다.
    /// GitHub Pages로 서빙하여 앱이 런타임에 번들을 다운로드할 수 있게 한다.
    /// </summary>
    public static class BundleUploadRunner
    {
        /// <summary>
        /// Remote group의 빌드 산출물을 release repo에 복사하고 git commit한다.
        /// </summary>
        /// <param name="settings">BuildAutomationSettings</param>
        /// <param name="ct">취소 토큰</param>
        /// <returns>성공 시 true</returns>
        public static async Task<bool> Run(
            BuildAutomationSettings settings,
            CancellationToken ct = default)
        {
            if (settings == null)
            {
                BuildAutomationLogger.LogError("[BundleUpload] Settings is null.");
                return false;
            }

            var aaSettings = AddressableAssetSettingsDefaultObject.Settings;
            if (aaSettings == null)
            {
                BuildAutomationLogger.LogError(
                    "[BundleUpload] AddressableAssetSettings not found.");
                return false;
            }

            // Release repo root 결정
            var releaseRoot = ResolveReleaseRoot(settings);
            var buildTarget = EditorUserBuildSettings.activeBuildTarget.ToString();
            var version = $"v{UnityEngine.Application.version}";
            var versionTarget = Path.Combine(version, buildTarget);  // v0.2.0/Android
            var destDir = Path.Combine(releaseRoot, versionTarget);

            // Remote group들의 빌드 경로를 수집 (중복 제거)
            var remoteBuildPaths = new HashSet<string>();
            foreach (var group in aaSettings.groups)
            {
                if (group == null) continue;
                if (!IsRemoteGroup(group)) continue;

                var schema = group.GetSchema<BundledAssetGroupSchema>();
                if (schema == null) continue;

                var resolved = schema.BuildPath.GetValue(aaSettings);
                if (!string.IsNullOrEmpty(resolved))
                {
                    remoteBuildPaths.Add(Path.GetFullPath(resolved));
                    BuildAutomationLogger.Log(
                        $"[BundleUpload] Remote group \"{group.Name}\" → {resolved}");
                }
            }

            if (remoteBuildPaths.Count == 0)
            {
                BuildAutomationLogger.LogWarning(
                    "[BundleUpload] No remote group found.");
                return false;
            }

            // 대상 디렉토리 clean & 생성
            if (Directory.Exists(destDir))
                Directory.Delete(destDir, true);
            Directory.CreateDirectory(destDir);

            // Remote group 빌드 경로의 파일을 전부 복사
            var copiedCount = 0;
            foreach (var srcDir in remoteBuildPaths)
            {
                if (!Directory.Exists(srcDir))
                {
                    BuildAutomationLogger.LogWarning(
                        $"[BundleUpload] Build path not found, skip: {srcDir}");
                    continue;
                }

                foreach (var file in Directory.GetFiles(srcDir))
                {
                    var fileName = Path.GetFileName(file);
                    var destFile = Path.Combine(destDir, fileName);
                    File.Copy(file, destFile, true);
                    copiedCount++;
                }
            }

            if (copiedCount == 0)
            {
                BuildAutomationLogger.LogWarning(
                    "[BundleUpload] No files found in remote build paths.");
                return false;
            }

            BuildAutomationLogger.Log(
                $"[BundleUpload] Copied {copiedCount} file(s) from {remoteBuildPaths.Count} remote path(s)");

            if (ct.IsCancellationRequested) return false;

            // git commit
            var commitMsg = $"chore: update addressable bundles ({versionTarget})";
            var files = new[] { version };

            BuildAutomationLogger.Log("[BundleUpload] Git commit...");
            var gitOk = await GitRunner.Commit(commitMsg, files, ct, releaseRoot);
            if (!gitOk && !ct.IsCancellationRequested)
            {
                BuildAutomationLogger.LogError("[BundleUpload] Git commit failed");
                return false;
            }

            BuildAutomationLogger.Log("[BundleUpload] Upload completed");
            return true;
        }

        /// <summary>
        /// Release repo root를 반환한다.
        /// releaseRepoRoot가 비어있으면 프로젝트 루트를 사용한다.
        /// </summary>
        private static string ResolveReleaseRoot(BuildAutomationSettings settings)
        {
            if (!string.IsNullOrEmpty(settings.releaseRepoRoot))
            {
                var root = settings.releaseRepoRoot;
                if (Path.IsPathRooted(root))
                    return Path.GetFullPath(root);

                var projectRoot = Path.GetFullPath(
                    Path.Combine(Application.dataPath, ".."));
                return Path.GetFullPath(Path.Combine(projectRoot, root));
            }

            return Path.GetFullPath(
                Path.Combine(Application.dataPath, ".."));
        }

        /// <summary>
        /// group이 Remote인지 판별한다.
        /// LoadPath가 http URL이면 Remote로 간주.
        /// </summary>
        private static bool IsRemoteGroup(AddressableAssetGroup group)
        {
            var schema = group.GetSchema<BundledAssetGroupSchema>();
            if (schema == null) return false;

            var settings = AddressableAssetSettingsDefaultObject.Settings;
            var loadPath = schema.LoadPath.GetValue(settings);
            return loadPath != null
                && (loadPath.StartsWith("http://") || loadPath.StartsWith("https://"));
        }
    }
}
