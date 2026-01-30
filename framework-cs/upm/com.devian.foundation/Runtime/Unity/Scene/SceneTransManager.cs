// SSOT: skills/devian-unity/30-unity-components/15-scene-trans-manager/SKILL.md

#nullable enable

using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Devian
{
    /// <summary>
    /// Scene 전환 파이프라인을 단일화(직렬화)하는 싱글턴.
    /// 전환 순서: FadeOut → beforeUnload → BaseScene.OnExit → Load → afterLoad → BaseScene.OnEnter → FadeIn
    /// 부팅 시 첫 씬의 OnEnter()도 1회 보장한다.
    ///
    /// 이 Manager는 페이드 UI를 직접 소유하지 않으며, FadeOutRequested/FadeInRequested 이벤트로 위임한다.
    /// </summary>
    public sealed class SceneTransManager : AutoSingleton<SceneTransManager>
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
        /// 페이드 아웃 요청 이벤트. 구독자는 fadeOutSeconds 동안 페이드 아웃을 수행하는 코루틴을 반환한다.
        /// </summary>
        public event Func<float, IEnumerator>? FadeOutRequested;

        /// <summary>
        /// 페이드 인 요청 이벤트. 구독자는 fadeInSeconds 동안 페이드 인을 수행하는 코루틴을 반환한다.
        /// </summary>
        public event Func<float, IEnumerator>? FadeInRequested;

        // ====================================================================
        // Lifecycle
        // ====================================================================

        protected override void Awake()
        {
            base.Awake();
        }

        /// <summary>
        /// 부팅 시 첫 씬의 OnEnter()를 1회 보장한다 (LoadSceneAsync를 거치지 않는 케이스).
        /// </summary>
        private IEnumerator Start()
        {
            // 전환 중이면 bootstrap 하지 않음
            if (_isTransitioning)
                yield break;

            var scene = FindActiveBaseScene();
            if (scene == null)
                yield break;

            // 이미 Enter 되었으면 스킵
            if (scene.HasEntered)
                yield break;

            scene._MarkEntered();
            yield return scene.OnEnter();
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
        /// <param name="beforeUnload">언로드 전 실행할 코루틴 (optional)</param>
        /// <param name="afterLoad">로드 후 실행할 코루틴 (optional)</param>
        /// <param name="onError">에러 발생 시 콜백 (optional)</param>
        public IEnumerator LoadSceneAsync(
            string sceneKey,
            LoadSceneMode mode = LoadSceneMode.Single,
            float fadeOutSeconds = 0.2f,
            float fadeInSeconds = 0.2f,
            Func<IEnumerator>? beforeUnload = null,
            Func<IEnumerator>? afterLoad = null,
            Action<string>? onError = null)
        {
            if (string.IsNullOrWhiteSpace(sceneKey))
            {
                Log.Error("SceneTransManager.LoadSceneAsync failed: sceneKey is null/empty.");
                onError?.Invoke("sceneKey is null/empty");
                yield break;
            }

            if (_isTransitioning)
            {
                Log.Warn("SceneTransManager.LoadSceneAsync ignored: already transitioning.");
                yield break;
            }

            _isTransitioning = true;

            // 1) FadeOut (이벤트 위임)
            if (fadeOutSeconds > 0f)
            {
                yield return InvokeFadeEvent(FadeOutRequested, fadeOutSeconds);
            }

            // 2) beforeUnload hook
            if (beforeUnload != null)
            {
                yield return beforeUnload();
            }

            // 3) Exit current scene (best-effort)
            var current = FindActiveBaseScene();
            if (current != null)
            {
                yield return current.OnExit();
            }

            // 4) Load next scene
            yield return AssetManager.LoadSceneAsync(sceneKey, mode, activateOnLoad: true, priority: 100);

            // 5) afterLoad hook
            if (afterLoad != null)
            {
                yield return afterLoad();
            }

            // 6) Enter next scene (best-effort, 중복 방지)
            var next = FindActiveBaseScene();
            if (next != null)
            {
                if (!next.HasEntered)
                {
                    next._MarkEntered();
                    yield return next.OnEnter();
                }
                else
                {
                    Log.Warn("SceneTransManager: OnEnter skipped (already entered).");
                }
            }

            // 7) FadeIn (이벤트 위임)
            if (fadeInSeconds > 0f)
            {
                yield return InvokeFadeEvent(FadeInRequested, fadeInSeconds);
            }

            _isTransitioning = false;
        }

        // ====================================================================
        // Internal Helpers
        // ====================================================================

        private BaseScene? FindActiveBaseScene()
        {
            // 활성 씬 root objects에서만 BaseScene 탐색 (정책: 1개 권장)
            var active = SceneManager.GetActiveScene();
            if (!active.IsValid())
                return null;

            var roots = active.GetRootGameObjects();
            BaseScene? first = null;
            int count = 0;

            for (int i = 0; i < roots.Length; i++)
            {
                var found = roots[i].GetComponentsInChildren<BaseScene>(includeInactive: true);
                if (found == null || found.Length == 0) continue;

                for (int j = 0; j < found.Length; j++)
                {
                    if (found[j] == null) continue;
                    if (first == null) first = found[j];
                    count++;
                }
            }

            if (count > 1)
                Log.Warn("SceneTransManager: multiple BaseScene found in active scene. Using the first one.");

            return first;
        }

        /// <summary>
        /// 이벤트에 등록된 모든 델리게이트를 순차 실행한다.
        /// </summary>
        private IEnumerator InvokeFadeEvent(Func<float, IEnumerator>? fadeEvent, float seconds)
        {
            if (fadeEvent == null)
                yield break;

            var invocationList = fadeEvent.GetInvocationList();
            for (int i = 0; i < invocationList.Length; i++)
            {
                var handler = invocationList[i] as Func<float, IEnumerator>;
                if (handler != null)
                {
                    var coroutine = handler(seconds);
                    if (coroutine != null)
                    {
                        yield return coroutine;
                    }
                }
            }
        }
    }
}
