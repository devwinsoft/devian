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

            var commitResult = await RunGitWithOutput($"commit -m \"{escapedMessage}\"", root, ct);
            if (!commitResult.success)
            {
                // "nothing to commit" 은 정상 — warning 처리
                if (commitResult.output != null
                    && commitResult.output.Contains("nothing to commit"))
                {
                    BuildAutomationLogger.LogWarning("[Git] Nothing to commit (no changes)");
                    return true;
                }

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

        private struct GitResult
        {
            public bool success;
            public string output;
        }

        private static async Task<GitResult> RunGitWithOutput(
            string arguments, string workingDirectory, CancellationToken ct)
        {
            var result = await RunGitInternal(arguments, workingDirectory, ct);
            return result;
        }

        private static async Task<bool> RunGit(
            string arguments, string workingDirectory, CancellationToken ct)
        {
            var result = await RunGitInternal(arguments, workingDirectory, ct);
            return result.success;
        }

        private static Task<GitResult> RunGitInternal(
            string arguments, string workingDirectory, CancellationToken ct)
        {
            var gitPath = ResolveGitPath();
            if (gitPath == null)
            {
                BuildAutomationLogger.LogError(
                    "[Git] git not found. PATH에 git이 포함되어 있는지 확인하세요.");
                return Task.FromResult(new GitResult { success = false });
            }

            try
            {
                var startInfo = BuildAutomationUtil.CreateStartInfo(
                    gitPath, arguments, workingDirectory);
                var process = new Process { StartInfo = startInfo };

                _currentProcess = process;
                process.Start();

                var stdout = process.StandardOutput.ReadToEnd();
                var stderr = process.StandardError.ReadToEnd();
                var allOutput = (stdout + "\n" + stderr).Trim();

                // 동기 대기 — await 금지 (player loop context switch 방지)
                process.WaitForExit(DefaultTimeoutMs);

                // 로그 출력
                foreach (var line in allOutput.Split('\n'))
                {
                    var trimmed = line.Trim();
                    if (!string.IsNullOrEmpty(trimmed))
                        BuildAutomationLogger.Log($"[Git] {trimmed}");
                }

                _currentProcess = null;

                if (!process.HasExited)
                {
                    process.Kill();
                    BuildAutomationLogger.LogError(
                        $"[Git] Timeout ({DefaultTimeoutMs / 1000}s)");
                    return Task.FromResult(
                        new GitResult { success = false, output = allOutput });
                }

                if (ct.IsCancellationRequested)
                {
                    BuildAutomationLogger.LogWarning("[Git] Cancelled by user");
                    return Task.FromResult(
                        new GitResult { success = false, output = allOutput });
                }

                if (process.ExitCode == 0)
                    return Task.FromResult(
                        new GitResult { success = true, output = allOutput });

                BuildAutomationLogger.LogError(
                    $"[Git] Command failed (exit code {process.ExitCode})");
                return Task.FromResult(
                    new GitResult { success = false, output = allOutput });
            }
            catch (System.Exception ex)
            {
                BuildAutomationLogger.LogError($"[Git] Failed to run git: {ex.Message}");
                return Task.FromResult(new GitResult { success = false });
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
