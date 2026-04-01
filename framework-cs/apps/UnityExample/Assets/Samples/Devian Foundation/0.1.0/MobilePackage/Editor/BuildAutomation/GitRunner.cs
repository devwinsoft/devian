using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace Devian
{
    /// <summary>
    /// git CLI를 System.Diagnostics.Process로 호출하는 래퍼.
    /// Version Publish(Release 탭)에서 사용한다.
    /// </summary>
    public static class GitRunner
    {
        /// <summary>기본 타임아웃 (밀리초). 2분.</summary>
        private const int DefaultTimeoutMs = 2 * 60 * 1000;

        /// <summary>현재 실행 중인 프로세스. Cancel 시 Kill에 사용.</summary>
        private static Process _currentProcess;

        /// <summary>
        /// 지정된 파일을 git add → commit 한다.
        /// push는 하지 않는다. 사용자가 직접 push 여부를 결정한다.
        /// </summary>
        /// <param name="commitMessage">커밋 메시지</param>
        /// <param name="filePaths">스테이징할 파일 경로 배열 (workingDirectory 기준 상대경로)</param>
        /// <param name="ct">취소 토큰</param>
        /// <param name="workingDirectory">git 실행 디렉토리. null이면 Unity 프로젝트 루트 사용</param>
        /// <returns>성공 시 true</returns>
        public static async Task<bool> Commit(
            string commitMessage,
            string[] filePaths,
            CancellationToken ct = default,
            string workingDirectory = null)
        {
            if (filePaths == null || filePaths.Length == 0)
            {
                BuildAutomationLogger.LogError("[Git] No files specified for commit");
                return false;
            }

            var root = workingDirectory ?? System.IO.Path.GetFullPath(
                System.IO.Path.Combine(UnityEngine.Application.dataPath, ".."));

            // 1. git add
            var addArgs = string.Join(" ", System.Array.ConvertAll(filePaths, p => $"\"{p}\""));
            BuildAutomationLogger.Log($"[Git] $ git add {addArgs}  (cwd: {root})");

            if (!await RunGit($"add {addArgs}", root, ct))
            {
                BuildAutomationLogger.LogError("[Git] git add failed");
                return false;
            }

            // 2. git commit
            var escapedMessage = commitMessage.Replace("\"", "\\\"");
            BuildAutomationLogger.Log($"[Git] $ git commit -m \"{commitMessage}\"");

            if (!await RunGit($"commit -m \"{escapedMessage}\"", root, ct))
            {
                BuildAutomationLogger.LogError("[Git] git commit failed");
                return false;
            }

            BuildAutomationLogger.Log("[Git] Commit succeeded");
            return true;
        }

        /// <summary>현재 실행 중인 프로세스를 강제 종료한다.</summary>
        public static void Kill()
        {
            try
            {
                if (_currentProcess != null && !_currentProcess.HasExited)
                {
                    _currentProcess.Kill();
                    BuildAutomationLogger.LogWarning("[Git] Process killed");
                }
            }
            catch (System.Exception ex)
            {
                BuildAutomationLogger.LogWarning($"[Git] Kill failed: {ex.Message}");
            }
            finally
            {
                _currentProcess = null;
            }
        }

        private static async Task<bool> RunGit(
            string arguments, string workingDirectory, CancellationToken ct)
        {
            var gitPath = ResolveGitPath();
            if (gitPath == null)
            {
                BuildAutomationLogger.LogError(
                    "[Git] git not found. PATH에 git이 포함되어 있는지 확인하세요.");
                return false;
            }

            try
            {
                var startInfo = BuildAutomationUtil.CreateStartInfo(
                    gitPath, arguments, workingDirectory);
                var process = new Process { StartInfo = startInfo };

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
                        BuildAutomationLogger.LogError($"[Git] Cancelled: {reason}");
                        return false;
                    }
                }

                _currentProcess = null;

                if (ct.IsCancellationRequested)
                {
                    BuildAutomationLogger.LogWarning("[Git] Cancelled by user");
                    return false;
                }

                if (process.ExitCode == 0)
                    return true;

                BuildAutomationLogger.LogError(
                    $"[Git] Command failed (exit code {process.ExitCode})");
                return false;
            }
            catch (System.Exception ex)
            {
                BuildAutomationLogger.LogError($"[Git] Failed to run git: {ex.Message}");
                return false;
            }
            finally
            {
                _currentProcess = null;
            }
        }

        /// <summary>git 실행 파일 경로를 찾는다.</summary>
        private static string ResolveGitPath()
        {
            var searchPaths = new[]
            {
                "/usr/bin/git",
                "/usr/local/bin/git",
                "/opt/homebrew/bin/git",
            };

            foreach (var p in searchPaths)
            {
                if (System.IO.File.Exists(p))
                    return p;
            }

            return null;
        }
    }
}
