using System.IO;
using UnityEditor;
using UnityEngine;

namespace Devian
{
    /// <summary>
    /// 빌드 자동화 공통 유틸리티.
    /// BuildAutomationSettings 로드, 사전 요건 검사, CLI 경로 해석을 제공한다.
    /// </summary>
    public static class BuildAutomationUtil
    {
        /// <summary>
        /// 프로젝트 내 BuildAutomationSettings 에셋을 검색하여 로드한다.
        /// 없으면 null을 반환하고 에러 로그를 남긴다.
        /// </summary>
        public static BuildAutomationSettings LoadSettings()
        {
            var guids = AssetDatabase.FindAssets("t:BuildAutomationSettings");
            if (guids.Length == 0)
            {
                Debug.LogError(
                    "[BuildAutomation] BuildAutomationSettings.asset not found. " +
                    "Create via Assets > Create > Devian > Build Automation Settings");
                return null;
            }

            var path = AssetDatabase.GUIDToAssetPath(guids[0]);
            var settings = AssetDatabase.LoadAssetAtPath<BuildAutomationSettings>(path);

            if (guids.Length > 1)
            {
                Debug.LogWarning(
                    $"[BuildAutomation] Multiple BuildAutomationSettings found. Using: {path}");
            }

            return settings;
        }

        // ─── CLI 경로 해석 ──────────────────────────────────

        /// <summary>firebase CLI의 일반적인 설치 경로 (macOS/Linux)</summary>
        private static readonly string[] FirebaseSearchPaths = new[]
        {
            "/usr/local/bin/firebase",
            "/opt/homebrew/bin/firebase",
            "{HOME}/.npm-global/bin/firebase",
            "{HOME}/.nvm/current/bin/firebase",
            "/usr/bin/firebase",
        };

        /// <summary>
        /// Firebase CLI 절대경로를 반환한다.
        /// 우선순위: Settings 지정값 → 일반 경로 탐색 → nvm 탐색
        /// 찾지 못하면 null.
        /// </summary>
        public static string ResolveFirebaseCLI(BuildAutomationSettings settings)
        {
            // 1. Settings에 명시적으로 지정된 경로
            if (!string.IsNullOrEmpty(settings.firebaseCLIPath) &&
                File.Exists(settings.firebaseCLIPath))
                return settings.firebaseCLIPath;

            // 2. 일반 경로 탐색
            var found = SearchPaths(FirebaseSearchPaths);
            if (found != null) return found;

            // 3. nvm 버전별 탐색 (가장 최신 버전 사용)
            var nvmPath = SearchNvmBin("firebase");
            return nvmPath;
        }

        /// <summary>
        /// 절대경로로 확정된 CLI를 실행하는 ProcessStartInfo를 생성한다.
        /// 쉘을 거치지 않고 직접 실행하되, Unity Editor가 상속하지 못하는
        /// PATH를 보충하여 node, ruby 등 런타임을 찾을 수 있게 한다.
        /// </summary>
        public static System.Diagnostics.ProcessStartInfo CreateStartInfo(
            string absolutePath, string arguments, string workingDirectory = null)
        {
            var startInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = absolutePath,
                Arguments = arguments,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            if (!string.IsNullOrEmpty(workingDirectory))
                startInfo.WorkingDirectory = workingDirectory;

            // Unity Editor(GUI 앱)는 쉘 PATH를 상속받지 못한다.
            // firebase CLI 등은 #!/usr/bin/env node 를 사용하므로
            // node가 포함된 경로를 PATH에 주입해야 한다.
            InjectPath(startInfo, absolutePath);

            // 외부 CLI 도구가 UTF-8 로케일을 요구한다.
            // Unity Editor 프로세스는 로케일 환경변수가 비어 있을 수 있다.
            InjectLocale(startInfo);

            return startInfo;
        }

        /// <summary>Unity 프로젝트 루트의 절대경로를 반환한다.</summary>
        public static string GetProjectRootPath()
        {
            // Application.dataPath = "{ProjectRoot}/Assets"
            return Path.GetDirectoryName(UnityEngine.Application.dataPath);
        }

        /// <summary>
        /// 외부 CLI 도구가 요구하는 UTF-8 로케일을 환경변수에 주입한다.
        /// </summary>
        private static void InjectLocale(System.Diagnostics.ProcessStartInfo startInfo)
        {
            if (string.IsNullOrEmpty(
                System.Environment.GetEnvironmentVariable("LC_ALL")))
            {
                startInfo.EnvironmentVariables["LC_ALL"] = "en_US.UTF-8";
            }
            if (string.IsNullOrEmpty(
                System.Environment.GetEnvironmentVariable("LANG")))
            {
                startInfo.EnvironmentVariables["LANG"] = "en_US.UTF-8";
            }
        }

        /// <summary>
        /// ProcessStartInfo의 PATH 환경변수에 node/ruby 등 런타임 경로를 주입한다.
        /// CLI 실행 파일의 부모 디렉토리(보통 node도 같이 있음)도 추가한다.
        /// </summary>
        private static void InjectPath(
            System.Diagnostics.ProcessStartInfo startInfo, string cliAbsolutePath)
        {
            var home = System.Environment.GetEnvironmentVariable("HOME") ?? "";
            var existingPath = System.Environment.GetEnvironmentVariable("PATH") ?? "";

            // node/ruby가 있을 수 있는 일반적인 경로들
            var extraPaths = new System.Collections.Generic.List<string>
            {
                "/usr/local/bin",
                "/opt/homebrew/bin",
                "/usr/bin",
                "/bin",
            };

            // CLI 실행 파일의 부모 디렉토리 (firebase와 node가 같은 bin에 있을 가능성)
            if (!string.IsNullOrEmpty(cliAbsolutePath))
            {
                var parentDir = Path.GetDirectoryName(cliAbsolutePath);
                if (!string.IsNullOrEmpty(parentDir) && !extraPaths.Contains(parentDir))
                    extraPaths.Insert(0, parentDir);
            }

            // nvm: 최신 node 버전의 bin 경로
            if (!string.IsNullOrEmpty(home))
            {
                var nvmNodeBin = FindLatestNvmNodeBin(home);
                if (nvmNodeBin != null && !extraPaths.Contains(nvmNodeBin))
                    extraPaths.Insert(0, nvmNodeBin);

                // npm global
                var npmGlobal = Path.Combine(home, ".npm-global/bin");
                if (Directory.Exists(npmGlobal) && !extraPaths.Contains(npmGlobal))
                    extraPaths.Add(npmGlobal);
            }

            // 기존 PATH에 없는 경로만 추가
            var pathSet = new System.Collections.Generic.HashSet<string>(
                existingPath.Split(':'));

            var toAdd = new System.Collections.Generic.List<string>();
            foreach (var p in extraPaths)
            {
                if (!string.IsNullOrEmpty(p) && Directory.Exists(p) && !pathSet.Contains(p))
                    toAdd.Add(p);
            }

            if (toAdd.Count > 0)
            {
                var injectedPath = string.Join(":", toAdd) + ":" + existingPath;
                startInfo.EnvironmentVariables["PATH"] = injectedPath;
            }
        }

        /// <summary>~/.nvm/versions/node/ 에서 최신 버전의 bin 경로를 반환한다.</summary>
        private static string FindLatestNvmNodeBin(string home)
        {
            var nvmVersionsDir = Path.Combine(home, ".nvm/versions/node");
            if (!Directory.Exists(nvmVersionsDir)) return null;

            var versionDirs = Directory.GetDirectories(nvmVersionsDir);
            if (versionDirs.Length == 0) return null;

            System.Array.Sort(versionDirs);
            System.Array.Reverse(versionDirs);

            var binDir = Path.Combine(versionDirs[0], "bin");
            return Directory.Exists(binDir) ? binDir : null;
        }

        // ─── 사전 요건 검사 ─────────────────────────────────

        /// <summary>
        /// 사전 요건을 검사하여 결과를 반환한다.
        /// GUI의 Settings 탭에서 상태 표시에 사용한다.
        /// </summary>
        public static PrerequisiteStatus CheckPrerequisites(BuildAutomationSettings settings)
        {
            var status = new PrerequisiteStatus();

            // 플랫폼 자동감지
            status.androidSupported = BuildPipeline.IsBuildTargetSupported(
                BuildTargetGroup.Android, BuildTarget.Android);
            status.iosSupported = BuildPipeline.IsBuildTargetSupported(
                BuildTargetGroup.iOS, BuildTarget.iOS);

            // Firebase SDK 확인
            status.firebaseSdkFound =
                AssetDatabase.IsValidFolder("Assets/Firebase") ||
                DoesUpmPackageExist("com.google.firebase.crashlytics");

            // Firebase App ID 확인
            status.firebaseAndroidAppIdSet =
                !string.IsNullOrEmpty(settings.firebaseAndroidAppId);
            status.firebaseIOSAppIdSet =
                !string.IsNullOrEmpty(settings.firebaseIOSAppId);

            // google-services.json (Android)
            if (status.androidSupported)
            {
                status.googleServicesJsonFound =
                    File.Exists("Assets/StreamingAssets/google-services.json") ||
                    File.Exists("google-services.json");
            }

            // GoogleService-Info.plist (iOS)
            if (status.iosSupported)
            {
                status.googleServiceInfoPlistFound =
                    AssetDatabase.FindAssets("GoogleService-Info t:TextAsset").Length > 0 ||
                    File.Exists("Assets/GoogleService-Info.plist");
            }

            // Firebase CLI — 절대경로로 확인
            var firebasePath = ResolveFirebaseCLI(settings);
            status.firebaseCLIAvailable = firebasePath != null;
            status.firebaseCLIResolvedPath = firebasePath ?? "(not found)";

            // Build output directory
            status.buildOutputDirValid =
                !string.IsNullOrEmpty(settings.buildOutputDir);

            return status;
        }

        // ─── 내부 유틸 ──────────────────────────────────────

        private static bool DoesUpmPackageExist(string packageName)
        {
            var manifestPath = "Packages/manifest.json";
            if (!File.Exists(manifestPath)) return false;
            var content = File.ReadAllText(manifestPath);
            return content.Contains(packageName);
        }

        /// <summary>
        /// 경로 배열에서 {HOME}을 치환하고 존재하는 첫 번째 경로를 반환한다.
        /// </summary>
        private static string SearchPaths(string[] paths)
        {
            var home = System.Environment.GetEnvironmentVariable("HOME") ?? "";

            foreach (var template in paths)
            {
                var path = template.Replace("{HOME}", home);
                if (File.Exists(path))
                    return path;
            }

            return null;
        }

        /// <summary>
        /// ~/.nvm/versions/node/ 아래에서 가장 최신 버전의 bin/{command}를 찾는다.
        /// nvm은 버전별로 node를 설치하므로, 버전 폴더를 역순 정렬하여 최신을 우선 사용한다.
        /// </summary>
        private static string SearchNvmBin(string command)
        {
            var home = System.Environment.GetEnvironmentVariable("HOME") ?? "";
            var nvmVersionsDir = Path.Combine(home, ".nvm/versions/node");

            if (!Directory.Exists(nvmVersionsDir))
                return null;

            // 버전 폴더를 역순 정렬하여 최신 버전 우선
            var versionDirs = Directory.GetDirectories(nvmVersionsDir);
            System.Array.Sort(versionDirs);
            System.Array.Reverse(versionDirs);

            foreach (var versionDir in versionDirs)
            {
                var binPath = Path.Combine(versionDir, "bin", command);
                if (File.Exists(binPath))
                    return binPath;
            }

            return null;
        }
    }

    /// <summary>사전 요건 검사 결과</summary>
    public struct PrerequisiteStatus
    {
        public bool firebaseSdkFound;
        public bool firebaseAndroidAppIdSet;
        public bool firebaseIOSAppIdSet;
        public bool googleServicesJsonFound;
        public bool googleServiceInfoPlistFound;
        public bool firebaseCLIAvailable;
        public string firebaseCLIResolvedPath;
        public bool buildOutputDirValid;
        public bool androidSupported;
        public bool iosSupported;

        public bool AllRequiredForBuild =>
            firebaseSdkFound && buildOutputDirValid;

        public bool AllRequiredForSymbolUpload =>
            AllRequiredForBuild && firebaseCLIAvailable;
    }
}
