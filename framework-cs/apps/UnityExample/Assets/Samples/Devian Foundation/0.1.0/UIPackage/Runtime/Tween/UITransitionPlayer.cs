using System;
using UnityEngine;

namespace Devian
{
    [RequireComponent(typeof(RectTransform))]
    public sealed class UITransitionPlayer : MonoBehaviour
    {
        private UITweenHandle _mainHandle;
        private Vector2 _groupAnchoredPositionBase;
        private RectTransform _targetRectTransform;
        private CanvasGroup _targetCanvasGroup;

        private void Reset()
        {
            CacheTargets();
        }

        private void Awake()
        {
            CacheTargets();
        }

        private void OnValidate()
        {
            CacheTargets();
        }

        private void OnDestroy()
        {
            Cancel();
        }

        public UITweenHandle Play(UITransitionPreset preset, Action onComplete = null)
        {
            if (preset == null)
            {
                Debug.LogWarning("[UITransitionPlayer] Play: preset is null.", this);
                return UITweenHandle.CreateCanceled();
            }

            Cancel();
            CacheTargets();
            WarnMissingTargets(preset);

            var runner = UITweenRunner.Instance;
            if (runner == null)
            {
                Debug.LogWarning("[UITransitionPlayer] Play: UITweenRunner is unavailable.", this);
                return UITweenHandle.CreateCanceled();
            }

            _mainHandle = runner.Play(this, preset, onComplete);
            return _mainHandle;
        }

        public UITweenHandle Play(UITransitionPresetAsset asset, Action onComplete = null)
        {
            if (asset == null)
            {
                Debug.LogWarning("[UITransitionPlayer] Play: preset asset is null.", this);
                return UITweenHandle.CreateCanceled();
            }

            return Play(asset.Preset, onComplete);
        }

        public UITweenHandle Play(UI_TRANSITION_PRESET_ID id, Action onComplete = null)
        {
            var asset = ResolvePresetAsset(id);
            if (asset == null)
            {
                return UITweenHandle.CreateCanceled();
            }

            return Play(asset, onComplete);
        }

        public UITweenHandle Play(UITweenSequence sequence, Action onComplete = null)
        {
            if (sequence == null || sequence.IsEmpty)
            {
                Debug.LogWarning("[UITransitionPlayer] Play: sequence is null or empty.", this);
                return UITweenHandle.CreateCanceled();
            }

            Cancel();
            CacheTargets();
            WarnMissingTargets(sequence);

            var runner = UITweenRunner.Instance;
            if (runner == null)
            {
                Debug.LogWarning("[UITransitionPlayer] Play: UITweenRunner is unavailable.", this);
                return UITweenHandle.CreateCanceled();
            }

            _mainHandle = runner.Play(this, sequence, onComplete);
            return _mainHandle;
        }

        public void Cancel()
        {
            if (_mainHandle == null)
            {
                return;
            }

            _mainHandle.Cancel();
            _mainHandle = null;
        }

        internal void BeginGroup()
        {
            CacheTargets();

            _groupAnchoredPositionBase = _targetRectTransform.anchoredPosition;
        }

        internal void ApplyFrom(UITransitionPreset preset)
        {
            if (preset == null)
            {
                return;
            }

            if (preset.UseAlpha && _targetCanvasGroup != null)
            {
                _targetCanvasGroup.alpha = preset.FromAlpha;
            }

            if (preset.UseAnchoredPosition)
            {
                _targetRectTransform.anchoredPosition = ResolveAnchoredPosition(preset.FromAnchoredPosition);
            }

            if (preset.UseScale)
            {
                transform.localScale = preset.FromScale;
            }
        }

        internal void ApplyAt(UITransitionPreset preset, float elapsed)
        {
            if (preset == null)
            {
                return;
            }

            var delay = Mathf.Max(0f, preset.Delay);
            if (elapsed < delay)
            {
                return;
            }

            var duration = Mathf.Max(0f, preset.Duration);
            if (duration <= 0f)
            {
                ApplyTo(preset);
                return;
            }

            var time = Mathf.Clamp01((elapsed - delay) / duration);
            var eased = UITweenEaseUtil.Evaluate(preset.Ease, time);

            if (preset.UseAlpha && _targetCanvasGroup != null)
            {
                _targetCanvasGroup.alpha = Mathf.LerpUnclamped(preset.FromAlpha, preset.ToAlpha, eased);
            }

            if (preset.UseAnchoredPosition)
            {
                _targetRectTransform.anchoredPosition = Vector2.LerpUnclamped(
                    ResolveAnchoredPosition(preset.FromAnchoredPosition),
                    ResolveAnchoredPosition(preset.ToAnchoredPosition),
                    eased);
            }

            if (preset.UseScale)
            {
                transform.localScale = Vector3.LerpUnclamped(
                    preset.FromScale,
                    preset.ToScale,
                    eased);
            }
        }

        internal void ApplyTo(UITransitionPreset preset)
        {
            if (preset == null)
            {
                return;
            }

            if (preset.UseAlpha && _targetCanvasGroup != null)
            {
                _targetCanvasGroup.alpha = preset.ToAlpha;
            }

            if (preset.UseAnchoredPosition)
            {
                _targetRectTransform.anchoredPosition = ResolveAnchoredPosition(preset.ToAnchoredPosition);
            }

            if (preset.UseScale)
            {
                transform.localScale = preset.ToScale;
            }
        }

        private void CacheTargets()
        {
            _targetRectTransform = transform as RectTransform;
            _targetCanvasGroup = GetComponent<CanvasGroup>();
        }

        private void WarnMissingTargets(UITweenSequence sequence)
        {
            for (var i = 0; i < sequence.GroupCount; i++)
            {
                var group = sequence.GetGroup(i);
                for (var j = 0; j < group.Count; j++)
                {
                    WarnMissingTargets(group[j]);
                }
            }
        }

        private void WarnMissingTargets(UITransitionPreset preset)
        {
            if (preset.UseAlpha && _targetCanvasGroup == null)
            {
                Debug.LogWarning("[UITransitionPlayer] CanvasGroup is missing on the same GameObject for alpha transition.", this);
            }

            // RectTransform is required on the same GameObject.
        }

        private Vector2 ResolveAnchoredPosition(Vector2 offset)
        {
            return _groupAnchoredPositionBase + offset;
        }

        private UITransitionPresetAsset ResolvePresetAsset(UI_TRANSITION_PRESET_ID id)
        {
            if (id == null || !id.IsValid)
            {
                Debug.LogWarning("[UITransitionPlayer] Preset ID is null or invalid.", this);
                return null;
            }

            var asset = AssetManager.GetAsset<UITransitionPresetAsset>(id.Value);
            if (asset == null)
            {
                Debug.LogWarning($"[UITransitionPlayer] Preset asset not found for id '{id.Value}'.", this);
                return null;
            }

            return asset;
        }
    }
}
