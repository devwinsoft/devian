// Unity Shared - Main Thread Helper
// SSOT: skills/devian-unity/10-base-system/09-unity-main-thread/SKILL.md
// NOTE: 이 파일은 Generated 폴더 산출물이 아닌 고정 유틸(수기 유지)이며,
//       정본은 upm 경로다. Packages는 복사본.

using System;
using System.Threading;
using UnityEngine;

namespace Devian
{
    /// <summary>
    /// Internal helper for main thread detection.
    /// Used by all Unity templates to enforce main thread constraint.
    /// 
    /// Initialization order:
    /// 1. SubsystemRegistration - earliest possible (before any [RuntimeInitializeOnLoadMethod])
    /// 2. InitIfNeeded() - called by AutoSingleton before EnsureOrThrow()
    /// </summary>
    internal static class UnityMainThread
    {
        // 0 = not initialized, >0 = main thread id
        private static int s_mainThreadId = 0;
        
        /// <summary>
        /// Earliest initialization point - SubsystemRegistration runs before BeforeSceneLoad.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void CaptureMainThread()
        {
            s_mainThreadId = Thread.CurrentThread.ManagedThreadId;
        }
        
        /// <summary>
        /// Initialize main thread ID if not already done.
        /// Safe to call multiple times.
        /// </summary>
        public static void InitIfNeeded()
        {
            if (s_mainThreadId == 0)
            {
                s_mainThreadId = Thread.CurrentThread.ManagedThreadId;
            }
        }
        
        /// <summary>
        /// Returns true if called from the main thread.
        /// Auto-initializes if not yet initialized.
        /// </summary>
        public static bool IsMainThread
        {
            get
            {
                InitIfNeeded();
                return s_mainThreadId == Thread.CurrentThread.ManagedThreadId;
            }
        }
        
        /// <summary>
        /// Throws InvalidOperationException if not on main thread.
        /// Auto-initializes if not yet initialized.
        /// </summary>
        /// <param name="context">Description for error message (e.g., "PoolManager.Spawn")</param>
        public static void EnsureOrThrow(string context)
        {
            InitIfNeeded();
            
            if (s_mainThreadId != Thread.CurrentThread.ManagedThreadId)
            {
                throw new InvalidOperationException(
                    $"[{context}] Must be called from the main thread. " +
                    "Unity API calls are not thread-safe.");
            }
        }
    }
}
