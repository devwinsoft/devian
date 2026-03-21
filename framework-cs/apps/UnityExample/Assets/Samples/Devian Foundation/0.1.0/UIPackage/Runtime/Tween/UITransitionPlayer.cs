using System;
using UnityEngine;

namespace Devian
{
    [RequireComponent(typeof(RectTransform))]
    public sealed class UITransitionPlayer : MonoBehaviour
    {
        private UITweenHandle _mainHandle;
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
            var compiled = UITransitionCompiler.Compile(preset);
            if (compiled == null || compiled.IsEmpty)
            {
                Debug.LogWarning("[UITransitionPlayer] Play: compiled transition is null or empty.", this);
                return UITweenHandle.CreateCanceled();
            }

            WarnMissingTargets(compiled);

            var runner = UITweenRunner.Instance;
            if (runner == null)
            {
                Debug.LogWarning("[UITransitionPlayer] Play: UITweenRunner is unavailable.", this);
                return UITweenHandle.CreateCanceled();
            }

            _mainHandle = runner.Play(this, compiled, onComplete);
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
            var compiled = UITransitionCompiler.Compile(sequence);
            if (compiled == null || compiled.IsEmpty)
            {
                Debug.LogWarning("[UITransitionPlayer] Play: compiled sequence is null or empty.", this);
                return UITweenHandle.CreateCanceled();
            }

            WarnMissingTargets(compiled);

            var runner = UITweenRunner.Instance;
            if (runner == null)
            {
                Debug.LogWarning("[UITransitionPlayer] Play: UITweenRunner is unavailable.", this);
                return UITweenHandle.CreateCanceled();
            }

            _mainHandle = runner.Play(this, compiled, onComplete);
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

        internal UITransitionSnapshot CaptureSnapshot()
        {
            CacheTargets();

            return new UITransitionSnapshot
            {
                BaseAlpha = _targetCanvasGroup != null ? _targetCanvasGroup.alpha : 1f,
                BaseAnchoredPosition = _targetRectTransform.anchoredPosition,
                BaseScale = transform.localScale
            };
        }

        internal void Apply(UITransitionFrameResult result)
        {
            if (result.HasAlpha && _targetCanvasGroup != null)
            {
                _targetCanvasGroup.alpha = result.Alpha;
            }

            if (result.HasAnchoredPosition)
            {
                _targetRectTransform.anchoredPosition = result.AnchoredPosition;
            }

            if (result.HasScale)
            {
                transform.localScale = result.Scale;
            }
        }

        private void CacheTargets()
        {
            _targetRectTransform = transform as RectTransform;
            _targetCanvasGroup = GetComponent<CanvasGroup>();
        }

        private void WarnMissingTargets(UICompiledTransitionData compiled)
        {
            if (compiled != null && compiled.UsesAlpha && _targetCanvasGroup == null)
            {
                Debug.LogWarning("[UITransitionPlayer] CanvasGroup is missing on the same GameObject for alpha transition.", this);
            }
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
