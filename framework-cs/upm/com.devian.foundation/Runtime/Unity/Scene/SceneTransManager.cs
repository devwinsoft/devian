// SSOT: skills/devian-unity/10-foundation/17-scene-trans-manager/SKILL.md

#nullable enable

using System;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Devian
{
    /// <summary>
    /// Scene 전환 파이프라인을 단일화(직렬화)하는 싱글턴.
    /// 전환 순서: FadeOut → beforeUnload → Exit → Load → afterLoad → Enter → FadeIn
    /// onStart는 각 SceneBase.Start()에서 호출된다.
    ///
    /// 이 Manager는 페이드 UI를 직접 소유하지 않으며, FadeOutRequested/FadeInRequested 이벤트로 위임한다.
    ///
    /// CompoSingleton-based: Bootstrap(부트 컨테이너)에 포함되어 등록된다.
    /// </summary>
    public sealed class SceneTransManager : CompoSingleton<SceneTransManager>
    {
        private bool _isTransitioning;

        /// <summary>
        /// 현재 전환 중인지 여부.
        /// </summary>
        public bool IsTransitioning => _isTransitioning;

        // ====================================================================
        // Fade 위임 이벤트 (페이드 UI는 외부 컴포넌트가 구독하여 처리)
        // ====================================================================

        /// <summary>
        /// 페이드 아웃 요청 이벤트. 구독자는 fadeOutSeconds 동안 페이드 아웃을 수행하는 Task를 반환한다.
        /// </summary>
        public event Func<float, Task>? FadeOutRequested;

        /// <summary>
        /// 페이드 인 요청 이벤트. 구독자는 fadeInSeconds 동안 페이드 인을 수행하는 Task를 반환한다.
        /// </summary>
        public event Func<float, Task>? FadeInRequested;

        // ====================================================================
        // Lifecycle
        // ====================================================================

        protected override void Awake()
        {
            base.Awake();
        }

        /// <summary>
        /// 부팅 시 첫 씬의 Enter()를 호출한다 (LoadSceneAsync를 거치지 않는 케이스).
        /// onStart는 SceneBase.Start()에서 호출된다.
        /// </summary>
        private void Start()
        {
            _ = StartAsync();
        }

        private async Task StartAsync()
        {
            if (_isTransitioning)
                return;

            var scene = FindActiveSceneBase();
            if (scene == null)
                return;

            try
            {
                // Enter 호출
                await scene.Enter();
            }
            catch (Exception ex)
            {
                Log.Error($"SceneTransManager.Start failed: {ex}");
            }
        }

        // ====================================================================
        // Core API
        // ====================================================================

        /// <summary>
        /// 지정된 씬을 로드한다.
        /// </summary>
        /// <param name="sceneKey">Addressables 씬 키</param>
        /// <param name="mode">씬 로드 모드 (기본: Single)</param>
        /// <param name="fadeOutSeconds">페이드 아웃 시간 (0 이하면 스킵)</param>
        /// <param name="fadeInSeconds">페이드 인 시간 (0 이하면 스킵)</param>
        /// <param name="beforeUnload">언로드 전 실행할 Task (optional)</param>
        /// <param name="afterLoad">로드 후 실행할 Task (optional)</param>
        /// <param name="onError">에러 발생 시 콜백 (optional)</param>
        public async Task LoadSceneAsync(
            string sceneKey,
            LoadSceneMode mode = LoadSceneMode.Single,
            float fadeOutSeconds = 0.2f,
            float fadeInSeconds = 0.2f,
            Func<Task>? beforeUnload = null,
            Func<Task>? afterLoad = null,
            Action<string>? onError = null)
        {
            if (string.IsNullOrWhiteSpace(sceneKey))
            {
                Log.Error("SceneTransManager.LoadSceneAsync failed: sceneKey is null/empty.");
                onError?.Invoke("sceneKey is null/empty");
                return;
            }

            if (_isTransitioning)
            {
                Log.Warn("SceneTransManager.LoadSceneAsync ignored: already transitioning.");
                return;
            }

            _isTransitioning = true;

            try
            {
                // 1) FadeOut (이벤트 위임)
                if (fadeOutSeconds > 0f)
                {
                    await InvokeFadeEvent(FadeOutRequested, fadeOutSeconds);
                }

                // 2) beforeUnload hook
                if (beforeUnload != null)
                {
                    await beforeUnload();
                }

                // 3) Exit current scene (best-effort)
                var current = FindActiveSceneBase();
                if (current != null)
                {
                    await current.Exit();
                }

                // 4) Load next scene (코루틴 브릿지)
                await UnityCoroutineRunner.RunAsync(this,
                    AssetManager.LoadSceneAsync(sceneKey, mode, activateOnLoad: true, priority: 100));

                // 5) afterLoad hook
                if (afterLoad != null)
                {
                    await afterLoad();
                }

                // 6) Enter next scene
                var next = FindActiveSceneBase();
                if (next != null)
                {
                    // Enter 호출
                    await next.Enter();
                }

                // 7) FadeIn (이벤트 위임)
                if (fadeInSeconds > 0f)
                {
                    await InvokeFadeEvent(FadeInRequested, fadeInSeconds);
                }
            }
            catch (Exception ex)
            {
                Log.Error($"SceneTransManager.LoadSceneAsync failed: {ex}");
                onError?.Invoke(ex.Message);
            }
            finally
            {
                _isTransitioning = false;
            }
        }

        // ====================================================================
        // Internal Helpers
        // ====================================================================

        private SceneBase? FindActiveSceneBase()
        {
            // 활성 씬 root objects에서만 SceneBase 탐색 (정책: 1개 권장)
            var active = SceneManager.GetActiveScene();
            if (!active.IsValid())
                return null;

            var roots = active.GetRootGameObjects();
            SceneBase? first = null;
            int count = 0;

            for (int i = 0; i < roots.Length; i++)
            {
                var found = roots[i].GetComponentsInChildren<SceneBase>(includeInactive: true);
                if (found == null || found.Length == 0) continue;

                for (int j = 0; j < found.Length; j++)
                {
                    if (found[j] == null) continue;
                    if (first == null) first = found[j];
                    count++;
                }
            }

            if (count > 1)
                Log.Warn("SceneTransManager: multiple SceneBase found in active scene. Using the first one.");

            return first;
        }

        /// <summary>
        /// 이벤트에 등록된 모든 델리게이트를 순차 실행한다.
        /// </summary>
        private async Task InvokeFadeEvent(Func<float, Task>? fadeEvent, float seconds)
        {
            if (fadeEvent == null)
                return;

            var invocationList = fadeEvent.GetInvocationList();
            for (int i = 0; i < invocationList.Length; i++)
            {
                var handler = invocationList[i] as Func<float, Task>;
                if (handler != null)
                {
                    await handler(seconds);
                }
            }
        }
    }
}
