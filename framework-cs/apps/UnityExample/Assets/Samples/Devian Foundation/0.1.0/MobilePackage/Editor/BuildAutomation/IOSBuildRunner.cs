using System;
using System.Diagnostics;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Devian
{
    /// <summary>
    /// Phase 1: iOS 빌드를 실행한다.
    /// Xcode 프로젝트를 Export한다.
    /// </summary>
    public static class IOSBuildRunner
    {
        /// <summary>
        /// iOS Xcode 프로젝트를 Export하고 BuildReport를 반환한다.
        /// </summary>
        public static BuildReport Run(BuildAutomationSettings settings)
        {
            BuildAutomationLogger.Log("[iOS] Build started...");

            var outputPath = $"{settings.buildOutputDir}/iOS";

            // 출력 디렉토리 생성
            if (!System.IO.Directory.Exists(outputPath))
            {
                System.IO.Directory.CreateDirectory(outputPath);
            }

            var buildOptions = BuildOptions.None;
            if (settings.developmentBuild)
                buildOptions |= BuildOptions.Development | BuildOptions.AllowDebugging;

            var options = new BuildPlayerOptions
            {
                scenes = EditorBuildSettings.scenes
                    .Where(s => s.enabled)
                    .Select(s => s.path).ToArray(),
                locationPathName = outputPath,
                target = BuildTarget.iOS,
                options = buildOptions
            };

            if (options.scenes.Length == 0)
            {
                BuildAutomationLogger.LogError(
                    "[iOS] No enabled scenes in Build Settings");
                return null;
            }

            BuildAutomationLogger.Log(
                $"[iOS] Output: {outputPath} ({options.scenes.Length} scenes)");

            var report = BuildPipeline.BuildPlayer(options);
            BuildReportAnalyzer.LogReport(report, "iOS");

            return report;
        }

        /// <summary>CLI batchmode 진입점</summary>
        public static void RunFromCLI()
        {
            var settings = BuildAutomationUtil.LoadSettings();
            if (settings == null)
                throw new Exception("BuildAutomationSettings not found");

            var report = Run(settings);
            if (report == null || report.summary.result != BuildResult.Succeeded)
            {
                EditorApplication.Exit(1);
            }
        }

        /// <summary>
        /// xcodebuild archive를 실행하여 .xcarchive와 dSYM을 생성한다.
        /// macOS에서만 동작한다.
        /// </summary>
        public static bool RunXcodeArchive(BuildAutomationSettings settings)
        {
#if !UNITY_EDITOR_OSX
            BuildAutomationLogger.LogWarning(
                "[iOS] xcodebuild archive is only available on macOS. Skipping.");
            return false;
#else
            var projectPath = $"{settings.buildOutputDir}/iOS/Unity-iPhone.xcodeproj";
            var archivePath = $"{settings.buildOutputDir}/iOS/app.xcarchive";

            if (!System.IO.Directory.Exists(projectPath))
            {
                BuildAutomationLogger.LogError(
                    $"[iOS] Xcode project not found: {projectPath}");
                return false;
            }

            BuildAutomationLogger.Log("[iOS] Starting xcodebuild archive...");

            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "xcodebuild",
                    Arguments =
                        $"-project \"{projectPath}\" " +
                        $"-scheme Unity-iPhone " +
                        $"-configuration Release " +
                        $"-archivePath \"{archivePath}\" " +
                        $"archive",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            process.Start();
            BuildAutomationLogger.StreamProcess(process);
            process.WaitForExit();

            if (process.ExitCode != 0)
            {
                BuildAutomationLogger.LogError(
                    $"[iOS] xcodebuild archive failed (exit code {process.ExitCode})");
                return false;
            }

            // dSYM 존재 확인
            var dsymDir = $"{archivePath}/dSYMs";
            if (System.IO.Directory.Exists(dsymDir))
            {
                var dsymFiles = System.IO.Directory.GetDirectories(dsymDir, "*.dSYM");
                BuildAutomationLogger.Log(
                    $"[iOS] Archive succeeded. dSYM count: {dsymFiles.Length}");
            }
            else
            {
                BuildAutomationLogger.LogWarning(
                    "[iOS] Archive succeeded but dSYMs directory not found");
            }

            return true;
#endif
        }
    }
}
