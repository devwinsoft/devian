using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Devian
{
    /// <summary>
    /// BuildReport를 분석하여 BuildAutomationLogger에 결과를 기록한다.
    /// 빌드 성공/실패, 용량, 소요 시간, 에러 메시지를 정리하여 출력한다.
    /// </summary>
    public static class BuildReportAnalyzer
    {
        public static void LogReport(BuildReport report, string platform)
        {
            if (report == null)
            {
                BuildAutomationLogger.LogError(
                    $"[{platform}] BuildReport is null — build may have been cancelled");
                return;
            }

            var summary = report.summary;

            if (summary.result == BuildResult.Succeeded)
            {
                var sizeMB = summary.totalSize / (1024.0 * 1024.0);
                BuildAutomationLogger.Log(
                    $"[{platform}] Build succeeded. " +
                    $"Size: {sizeMB:F1} MB, " +
                    $"Time: {summary.totalTime.TotalSeconds:F1}s, " +
                    $"Warnings: {summary.totalWarnings}");
            }
            else
            {
                BuildAutomationLogger.LogError(
                    $"[{platform}] Build failed ({summary.result}). " +
                    $"Errors: {summary.totalErrors}, " +
                    $"Warnings: {summary.totalWarnings}");

                // 에러 메시지 상세 출력
                foreach (var step in report.steps)
                {
                    foreach (var msg in step.messages)
                    {
                        if (msg.type == LogType.Error || msg.type == LogType.Exception)
                        {
                            BuildAutomationLogger.LogError($"  {msg.content}");
                        }
                    }
                }
            }
        }
    }
}
