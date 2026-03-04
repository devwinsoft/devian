// Unity Shared - UnityTaskRunner
// SSOT: skills/devian-unity/11-common-system/32-unity-utils/SKILL.md
// NOTE: 이 파일은 Generated 폴더 산출물이 아닌 고정 유틸(수기 유지)이며,
//       정본은 upm 경로다. Packages는 복사본.

using System;
using System.Threading.Tasks;
using UnityEngine;

namespace Devian
{
    /// <summary>
    /// Observes fire-and-forget Task execution from Unity void entry points.
    /// </summary>
    public static class UnityTaskRunner
    {
        /// <summary>
        /// Runs an already-created Task and logs unhandled exceptions.
        /// </summary>
        public static void Run(Task task, string operation)
        {
            if (task == null) throw new ArgumentNullException(nameof(task));
            if (string.IsNullOrWhiteSpace(operation)) throw new ArgumentException("operation is null/empty", nameof(operation));

            _ = ObserveAsync(task, operation);
        }

        /// <summary>
        /// Creates a Task, then observes it and logs unhandled exceptions.
        /// Also catches synchronous exceptions thrown before a Task is returned.
        /// </summary>
        public static void Run(Func<Task> taskFactory, string operation)
        {
            if (taskFactory == null) throw new ArgumentNullException(nameof(taskFactory));
            if (string.IsNullOrWhiteSpace(operation)) throw new ArgumentException("operation is null/empty", nameof(operation));

            try
            {
                Run(taskFactory(), operation);
            }
            catch (Exception ex)
            {
                LogFailure(operation, ex);
            }
        }

        private static async Task ObserveAsync(Task task, string operation)
        {
            try
            {
                await task;
            }
            catch (Exception ex)
            {
                LogFailure(operation, ex);
            }
        }

        private static void LogFailure(string operation, Exception ex)
        {
            Debug.LogError($"{operation} failed: {ex}");
        }
    }
}
