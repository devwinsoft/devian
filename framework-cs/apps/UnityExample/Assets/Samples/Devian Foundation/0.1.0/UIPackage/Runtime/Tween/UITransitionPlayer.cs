using System;
using UnityEngine;
using UnityEngine.UI;

namespace Devian
{
    [RequireComponent(typeof(RectTransform))]
    public sealed class UITransitionPlayer : MonoBehaviour
    {
        private UITweenHandle _mainHandle;
        private RectTransform _targetRectTransform;
        private CanvasGroup _targetCanvasGroup;
        private LayoutElement _targetLayoutElement;
        private Vector2 _baselineAnchoredPosition;
        private bool _hasBaselineAnchoredPosition;

        private void Reset()
        {
            CacheTargets();
            RefreshBaseline();
        }

        private void Awake()
        {
            CacheTargets();
            RefreshBaseline();
        }

        private void OnValidate()
        {
            CacheTargets();
            RefreshBaseline();
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

        public void RefreshBaseline()
        {
            CacheTargets();
            if (_targetRectTransform == null)
            {
                _hasBaselineAnchoredPosition = false;
                _baselineAnchoredPosition = Vector2.zero;
                return;
            }

            _baselineAnchoredPosition = _targetRectTransform.anchoredPosition;
            _hasBaselineAnchoredPosition = true;
        }

        internal UITransitionSnapshot CaptureSnapshot()
        {
            CacheTargets();

            return new UITransitionSnapshot
            {
                BaseAlpha = _targetCanvasGroup != null ? _targetCanvasGroup.alpha : 1f,
                BaseAnchoredPosition = _hasBaselineAnchoredPosition
                    ? _baselineAnchoredPosition
                    : _targetRectTransform.anchoredPosition,
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

            if (result.HasPreferredSize && _targetLayoutElement != null)
            {
                _targetLayoutElement.preferredWidth = result.PreferredSize.x;
                _targetLayoutElement.preferredHeight = result.PreferredSize.y;
                LayoutRebuilder.MarkLayoutForRebuild(_targetRectTransform);

                if (_targetRectTransform.parent is RectTransform parentRectTransform)
                {
                    LayoutRebuilder.MarkLayoutForRebuild(parentRectTransform);
                }
            }
        }

        private void CacheTargets()
        {
            _targetRectTransform = transform as RectTransform;
            _targetCanvasGroup = GetComponent<CanvasGroup>();
            _targetLayoutElement = GetComponent<LayoutElement>();
        }

        private void WarnMissingTargets(UICompiledTransitionData compiled)
        {
            if (compiled != null && compiled.UsesAlpha && _targetCanvasGroup == null)
            {
                Debug.LogWarning("[UITransitionPlayer] CanvasGroup is missing on the same GameObject for alpha transition.", this);
            }

            if (compiled != null && compiled.UsesPreferredSize && _targetLayoutElement == null)
            {
                Debug.LogWarning("[UITransitionPlayer] LayoutElement is missing on the same GameObject for preferred size transition.", this);
            }
        }

        private UITransitionPresetAsset ResolvePresetAsset(UI_TRANSITION_PRESET_ID id)
        {
            if (id == null || !id.IsValid)
            {
                Debug.LogWarning("[UITransitionPlayer] Preset ID is null or invalid.", this);
                return null;
            }

#if UNITY_EDITOR
            var editorAsset = ResolveEditorPresetAsset(id.Value);
            if (editorAsset != null)
            {
                return editorAsset;
            }
#endif

            var asset = AssetManager.GetAsset<UITransitionPresetAsset>(id.Value);
            if (asset == null)
            {
                Debug.LogWarning($"[UITransitionPlayer] Preset asset not found for id '{id.Value}'.", this);
                return null;
            }

            return asset;
        }

#if UNITY_EDITOR
        private UITransitionPresetAsset ResolveEditorPresetAsset(string assetId)
        {
            if (string.IsNullOrWhiteSpace(assetId))
            {
                return null;
            }

            var settings = Resources.Load<UISettings>(UISettings.ResourcesPath);
            var searchDir = settings != null ? settings.GetSearchDir("UI_TRANSITION_PRESET_ID") : null;
            var assets = string.IsNullOrWhiteSpace(searchDir)
                ? AssetManager.FindAssets<UITransitionPresetAsset>(assetId)
                : AssetManager.FindAssets<UITransitionPresetAsset>(assetId, searchDir);

            if (assets == null || assets.Length == 0)
            {
                return null;
            }

            if (assets.Length > 1)
            {
                Debug.LogWarning($"[UITransitionPlayer] Multiple preset assets matched id '{assetId}'. Using the first match.", this);
            }

            return assets[0];
        }
#endif
    }
}
