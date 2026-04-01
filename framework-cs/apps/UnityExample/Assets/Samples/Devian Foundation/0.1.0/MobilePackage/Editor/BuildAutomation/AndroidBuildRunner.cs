using System;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Devian
{
    /// <summary>
    /// Phase 1: Android 빌드를 실행한다.
    /// BuildAutomationSettings에서 읽은 설정을 적용하여
    /// IL2CPP AAB/APK + symbols.zip을 생성한다.
    /// </summary>
    public static class AndroidBuildRunner
    {
        /// <summary>
        /// Android 빌드를 실행하고 BuildReport를 반환한다.
        /// CLI에서도 호출 가능: Unity -batchmode -executeMethod Devian.AndroidBuildRunner.RunFromCLI
        /// </summary>
        public static BuildReport Run(BuildAutomationSettings settings)
        {
            BuildAutomationLogger.Log("[Android] Build started...");

            // 빌드 설정 적용
            ApplySettings(settings);

            // 출력 경로 — AAB/APK 판정은 EditorUserBuildSettings에서 읽는다
            var isAppBundle = EditorUserBuildSettings.buildAppBundle;
            var ext = isAppBundle ? "aab" : "apk";
            var outputPath = $"{settings.buildOutputDir}/Android/{Application.productName}.{ext}";

            // 출력 디렉토리 생성
            var outputDir = System.IO.Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(outputDir) && !System.IO.Directory.Exists(outputDir))
            {
                System.IO.Directory.CreateDirectory(outputDir);
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
                target = BuildTarget.Android,
                options = buildOptions
            };

            if (options.scenes.Length == 0)
            {
                BuildAutomationLogger.LogError(
                    "[Android] No enabled scenes in Build Settings");
                return null;
            }

            BuildAutomationLogger.Log(
                $"[Android] Output: {outputPath} ({options.scenes.Length} scenes)");

            var report = BuildPipeline.BuildPlayer(options);
            BuildReportAnalyzer.LogReport(report, "Android");

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

        private static void ApplySettings(BuildAutomationSettings settings)
        {
            // Scripting backend: IL2CPP
            PlayerSettings.SetScriptingBackend(
                BuildTargetGroup.Android, ScriptingImplementation.IL2CPP);

            // AAB / APK — EditorUserBuildSettings.buildAppBundle을 그대로 사용한다
            // (Pipeline 탭에서 설정)

            // symbols.zip 생성 (Debugging 레벨)
            EditorUserBuildSettings.androidCreateSymbols =
                AndroidCreateSymbols.Debugging;

            // Target architectures
            var architectures = AndroidArchitecture.ARM64;
            if (settings.includeARMv7)
                architectures |= AndroidArchitecture.ARMv7;
            PlayerSettings.Android.targetArchitectures = architectures;

            // Keystore (비어있으면 debug keystore)
            if (!string.IsNullOrEmpty(settings.keystorePath))
            {
                PlayerSettings.Android.keystoreName = settings.keystorePath;
                // keystorePass, keyaliasName, keyaliasPass는
                // 환경변수에서 로드하거나 별도 보안 저장소 사용
                var keystorePass = Environment.GetEnvironmentVariable("ANDROID_KEYSTORE_PASS");
                var keyaliasName = Environment.GetEnvironmentVariable("ANDROID_KEYALIAS_NAME");
                var keyaliasPass = Environment.GetEnvironmentVariable("ANDROID_KEYALIAS_PASS");

                if (!string.IsNullOrEmpty(keystorePass))
                    PlayerSettings.Android.keystorePass = keystorePass;
                if (!string.IsNullOrEmpty(keyaliasName))
                    PlayerSettings.Android.keyaliasName = keyaliasName;
                if (!string.IsNullOrEmpty(keyaliasPass))
                    PlayerSettings.Android.keyaliasPass = keyaliasPass;
            }

            BuildAutomationLogger.Log(
                $"[Android] Settings applied — " +
                $"IL2CPP, {(EditorUserBuildSettings.buildAppBundle ? "AAB" : "APK")}, " +
                $"Arch: {architectures}, " +
                $"Symbols: Debugging, " +
                $"Dev: {settings.developmentBuild}");
        }
    }
}
