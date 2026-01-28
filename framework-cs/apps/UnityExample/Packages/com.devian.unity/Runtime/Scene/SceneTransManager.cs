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
    /// 전환 순서: FadeOut → BaseScene.OnExit → Load → BaseScene.OnEnter → FadeIn
    /// 부팅 시 첫 씬의 OnEnter()도 1회 보장한다.
    /// </summary>
    public sealed class SceneTransManager : MonoBehaviour
    {
        public static SceneTransManager? Instance { get; private set; }

        [Header("Overlay (optional)")]
        [SerializeField] private CanvasGroup? _overlay;
        [SerializeField] private float _fadeOutSeconds = 0.2f;
        [SerializeField] private float _fadeInSeconds = 0.2f;
        [SerializeField] private bool _dontDestroyOnLoad = true;

        private bool _isTransitioning;

        /// <summary>
        /// 현재 전환 중인지 여부.
        /// </summary>
        public bool IsTransitioning => _isTransitioning;

        /// <summary>
        /// Scene 전환 옵션.
        /// </summary>
        public struct SceneTransOptions
        {
            public LoadSceneMode Mode;
            public bool ActivateOnLoad;
            public int Priority;
            public bool UseFade;
            public bool BlockInput;
            public float FadeOutSecondsOverride;
            public float FadeInSecondsOverride;

            /// <summary>
            /// Single 모드 기본 옵션.
            /// </summary>
            public static SceneTransOptions DefaultSingle => new SceneTransOptions
            {
                Mode = LoadSceneMode.Single,
                ActivateOnLoad = true,
                Priority = 100,
                UseFade = true,
                BlockInput = true,
                FadeOutSecondsOverride = 0f,
                FadeInSecondsOverride = 0f,
            };
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;

            if (_dontDestroyOnLoad)
                DontDestroyOnLoad(gameObject);

            if (_overlay != null)
            {
                _overlay.alpha = 0f;
                _overlay.blocksRaycasts = false;
                _overlay.interactable = false;
            }
        }

        /// <summary>
        /// 부팅 시 첫 씬의 OnEnter()를 1회 보장한다 (TransitionTo를 거치지 않는 케이스).
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

        /// <summary>
        /// 지정된 씬으로 전환한다.
        /// </summary>
        /// <param name="sceneKey">Addressables 씬 키</param>
        /// <param name="options">전환 옵션</param>
        /// <param name="onError">에러 발생 시 콜백 (optional, 현재 미사용)</param>
        public IEnumerator TransitionTo(string sceneKey, SceneTransOptions options, Action<string>? onError = null)
        {
            if (string.IsNullOrWhiteSpace(sceneKey))
            {
                Log.Error("SceneTransManager.TransitionTo failed: sceneKey is null/empty.");
                yield break;
            }

            if (_isTransitioning)
            {
                Log.Warn("SceneTransManager.TransitionTo ignored: already transitioning.");
                yield break;
            }

            _isTransitioning = true;

            var fadeOut = options.FadeOutSecondsOverride > 0f ? options.FadeOutSecondsOverride : _fadeOutSeconds;
            var fadeIn = options.FadeInSecondsOverride > 0f ? options.FadeInSecondsOverride : _fadeInSeconds;

            // 1) Block input + Fade out
            if (options.BlockInput) SetOverlayBlocking(true);
            if (options.UseFade) yield return FadeTo(1f, fadeOut);

            // 2) Exit current scene (best-effort)
            var current = FindActiveBaseScene();
            if (current != null)
                yield return current.OnExit();

            // 3) Load next scene
            yield return AssetManager.LoadSceneAsync(sceneKey, options.Mode, options.ActivateOnLoad, options.Priority);

            // 4) Enter next scene (best-effort, 중복 방지)
            //    Single 모드면 Unity가 활성 씬을 전환하므로, Find로 새 BaseScene을 찾는다.
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

            // 5) Fade in + Unblock input
            if (options.UseFade) yield return FadeTo(0f, fadeIn);
            if (options.BlockInput) SetOverlayBlocking(false);

            _isTransitioning = false;
        }

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

        private void SetOverlayBlocking(bool on)
        {
            if (_overlay == null) return;
            _overlay.blocksRaycasts = on;
            _overlay.interactable = on;
        }

        private IEnumerator FadeTo(float targetAlpha, float seconds)
        {
            if (_overlay == null) yield break;

            seconds = Mathf.Max(0f, seconds);
            var start = _overlay.alpha;

            if (seconds <= 0f)
            {
                _overlay.alpha = targetAlpha;
                yield break;
            }

            float t = 0f;
            while (t < seconds)
            {
                t += Time.unscaledDeltaTime;
                var p = Mathf.Clamp01(t / seconds);
                _overlay.alpha = Mathf.Lerp(start, targetAlpha, p);
                yield return null;
            }

            _overlay.alpha = targetAlpha;
        }
    }
}
