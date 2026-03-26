using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Devian
{
    /// <summary>
    /// Phase 2: Firebase Crashlytics에 심볼 파일을 업로드한다.
    /// Android는 symbols.zip, iOS는 dSYM을 Firebase CLI로 업로드한다.
    /// CancellationToken으로 취소 가능하며, 타임아웃(기본 5분)을 지원한다.
    /// </summary>
    public static class SymbolUploader
    {
        /// <summary>기본 타임아웃 (밀리초). 5분.</summary>
        private const int DefaultTimeoutMs = 5 * 60 * 1000;

        /// <summary>현재 실행 중인 프로세스. Cancel 시 Kill에 사용.</summary>
        private static Process _currentProcess;

        /// <summary>
        /// Android symbols.zip을 Firebase에 업로드한다.
        /// </summary>
        /// <param name="settings">빌드 자동화 설정</param>
        /// <param name="symbolsZipPath">symbols.zip 절대 경로</param>
        /// <param name="ct">취소 토큰</param>
        public static async Task<bool> UploadAndroid(
            BuildAutomationSettings settings,
            string symbolsZipPath,
            CancellationToken ct = default)
        {
            if (string.IsNullOrEmpty(settings.firebaseAndroidAppId))
            {
                BuildAutomationLogger.LogError(
                    "[SymbolUpload] Firebase Android App ID not set in BuildAutomationSettings");
                return false;
            }

            if (string.IsNullOrEmpty(symbolsZipPath) || !File.Exists(symbolsZipPath))
            {
                BuildAutomationLogger.LogError(
                    $"[SymbolUpload] Android symbol file not found: {symbolsZipPath}");
                return false;
            }

            // 파일 크기 로그
            LogFileSize(symbolsZipPath);

            BuildAutomationLogger.Log(
                $"[SymbolUpload] Uploading Android symbols: {symbolsZipPath}");

            return await RunFirebaseCLI(settings,
                $"crashlytics:symbols:upload " +
                $"--app={settings.firebaseAndroidAppId} " +
                $"\"{symbolsZipPath}\"",
                ct);
        }

        /// <summary>
        /// iOS dSYM을 Firebase에 업로드한다.
        /// </summary>
        /// <param name="settings">빌드 자동화 설정</param>
        /// <param name="dsymPath">dSYMs 디렉토리 절대 경로</param>
        /// <param name="ct">취소 토큰</param>
        public static async Task<bool> UploadIOS(
            BuildAutomationSettings settings,
            string dsymPath,
            CancellationToken ct = default)
        {
            if (string.IsNullOrEmpty(settings.firebaseIOSAppId))
            {
                BuildAutomationLogger.LogError(
                    "[SymbolUpload] Firebase iOS App ID not set in BuildAutomationSettings");
                return false;
            }

            if (string.IsNullOrEmpty(dsymPath) || !Directory.Exists(dsymPath))
            {
                BuildAutomationLogger.LogError(
                    $"[SymbolUpload] dSYMs directory not found: {dsymPath}");
                return false;
            }

            // 디렉토리 크기 로그
            LogDirectorySize(dsymPath);

            BuildAutomationLogger.Log(
                $"[SymbolUpload] Uploading iOS dSYMs from: {dsymPath}");

            return await RunFirebaseCLI(settings,
                $"crashlytics:symbols:upload " +
                $"--app={settings.firebaseIOSAppId} " +
                $"\"{dsymPath}\"",
                ct);
        }

        /// <summary>현재 실행 중인 프로세스를 강제 종료한다.</summary>
        public static void Kill()
        {
            try
            {
                if (_currentProcess != null && !_currentProcess.HasExited)
                {
                    _currentProcess.Kill();
                    BuildAutomationLogger.LogWarning("[SymbolUpload] Process killed");
                }
            }
            catch (System.Exception ex)
            {
                BuildAutomationLogger.LogWarning(
                    $"[SymbolUpload] Kill failed: {ex.Message}");
            }
            finally
            {
                _currentProcess = null;
            }
        }

        private static async Task<bool> RunFirebaseCLI(
            BuildAutomationSettings settings, string arguments,
            CancellationToken ct)
        {
            var firebasePath = BuildAutomationUtil.ResolveFirebaseCLI(settings);
            if (firebasePath == null)
            {
                BuildAutomationLogger.LogError(
                    "[SymbolUpload] Firebase CLI not found. " +
                    "Settings > CLI Paths > Firebase CLI Path에 절대경로를 지정하세요. " +
                    "터미널에서 'which firebase'로 경로를 확인할 수 있습니다.");
                return false;
            }

            BuildAutomationLogger.Log($"[SymbolUpload] $ {firebasePath} {arguments}");
            BuildAutomationLogger.Log(
                $"[SymbolUpload] Timeout: {DefaultTimeoutMs / 1000}s. " +
                "Cancel 버튼으로 중단 가능.");

            try
            {
                var process = new Process
                {
                    StartInfo = BuildAutomationUtil.CreateStartInfo(firebasePath, arguments)
                };

                _currentProcess = process;
                process.Start();
                BuildAutomationLogger.StreamProcess(process);

                // CancellationToken과 타임아웃을 결합
                using (var timeoutCts = new CancellationTokenSource(DefaultTimeoutMs))
                using (var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
                    ct, timeoutCts.Token))
                {
                    try
                    {
                        await Task.Run(() =>
                        {
                            while (!process.HasExited)
                            {
                                if (linkedCts.Token.IsCancellationRequested)
                                {
                                    process.Kill();
                                    return;
                                }
                                Thread.Sleep(500);
                            }
                        }, linkedCts.Token);
                    }
                    catch (System.OperationCanceledException)
                    {
                        if (!process.HasExited)
                            process.Kill();

                        var reason = ct.IsCancellationRequested
                            ? "사용자 취소"
                            : $"타임아웃 ({DefaultTimeoutMs / 1000}초)";
                        BuildAutomationLogger.LogError(
                            $"[SymbolUpload] Upload cancelled: {reason}");
                        return false;
                    }
                }

                _currentProcess = null;

                if (ct.IsCancellationRequested)
                {
                    BuildAutomationLogger.LogWarning("[SymbolUpload] Cancelled by user");
                    return false;
                }

                if (process.ExitCode == 0)
                {
                    BuildAutomationLogger.Log("[SymbolUpload] Upload succeeded");
                    return true;
                }

                BuildAutomationLogger.LogError(
                    $"[SymbolUpload] Upload failed (exit code {process.ExitCode})");
                return false;
            }
            catch (System.Exception ex)
            {
                BuildAutomationLogger.LogError(
                    $"[SymbolUpload] Failed to run firebase CLI: {ex.Message}");
                return false;
            }
            finally
            {
                _currentProcess = null;
            }
        }

        private static void LogFileSize(string filePath)
        {
            try
            {
                var info = new FileInfo(filePath);
                var sizeMB = info.Length / (1024.0 * 1024.0);
                BuildAutomationLogger.Log(
                    $"[SymbolUpload] File size: {sizeMB:F1} MB ({info.Length:N0} bytes)");
            }
            catch { /* ignore */ }
        }

        private static void LogDirectorySize(string dirPath)
        {
            try
            {
                long totalSize = 0;
                var files = Directory.GetFiles(dirPath, "*", SearchOption.AllDirectories);
                foreach (var f in files)
                    totalSize += new FileInfo(f).Length;

                var sizeMB = totalSize / (1024.0 * 1024.0);
                BuildAutomationLogger.Log(
                    $"[SymbolUpload] dSYM total size: {sizeMB:F1} MB " +
                    $"({files.Length} files, {totalSize:N0} bytes)");
            }
            catch { /* ignore */ }
        }

    }
}
