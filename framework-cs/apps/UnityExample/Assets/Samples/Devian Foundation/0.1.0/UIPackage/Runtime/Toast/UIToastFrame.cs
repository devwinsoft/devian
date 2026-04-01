using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Devian
{
    [RequireComponent(typeof(UITransitionPlayer))]
    public sealed class UIToastFrame : UIBaseFrame, IPoolable
    {
        private struct LayoutSnapshot
        {
            public Vector2 AnchorMin;
            public Vector2 AnchorMax;
            public Vector2 Pivot;
            public Vector2 AnchoredPosition;
            public Vector2 SizeDelta;
            public Vector3 LocalScale;
        }

        [Header("Targets")]
        [SerializeField] private TextMeshProUGUI _messageText;
        [SerializeField] private UI_TRANSITION_PRESET_ID _showTransitionId;
        [SerializeField] private UI_TRANSITION_PRESET_ID _hideTransitionId;
        [SerializeField] private Graphic _backgroundGraphic;
        [SerializeField] private CanvasGroup _canvasGroup;

        [Header("Background Colors")]
        [SerializeField] private Color _infoBackgroundColor = new Color(0.15f, 0.15f, 0.15f, 0.95f);
        [SerializeField] private Color _successBackgroundColor = new Color(0.16f, 0.45f, 0.24f, 0.95f);
        [SerializeField] private Color _warningBackgroundColor = new Color(0.75f, 0.5f, 0.08f, 0.95f);
        [SerializeField] private Color _errorBackgroundColor = new Color(0.7f, 0.18f, 0.18f, 0.95f);

        [Header("Text Colors")]
        [SerializeField] private Color _infoTextColor = Color.white;
        [SerializeField] private Color _successTextColor = Color.white;
        [SerializeField] private Color _warningTextColor = Color.white;
        [SerializeField] private Color _errorTextColor = Color.white;

        private Coroutine _lifetimeCoroutine;
        private Action<UIToastFrame> _onHidden;
        private string _currentMessage = string.Empty;
        private LayoutSnapshot _initialLayout;
        private bool _hasInitialLayout;

        public bool isHiding { get; private set; }

        protected override void onAwake()
        {
            CacheInitialLayout();
            ResolveDefaults();
        }

        protected override void onInit()
        {
            CacheInitialLayout();
            ResolveDefaults();
        }

        protected override void onPoolSpawned()
        {
            ResolveDefaults();
            CancelInternal();
            isHiding = false;
            ApplyNonBlocking();
        }

        protected override void onPoolDespawned()
        {
            CancelInternal();
            isHiding = false;

            if (_messageText != null)
            {
                _messageText.text = string.Empty;
            }

            _currentMessage = string.Empty;
        }

        public void OnPoolSpawned()
        {
            _HandlePoolSpawned();
        }

        public void OnPoolDespawned()
        {
            _HandlePoolDespawned();
        }

        public void Bind(string message, ToastType toastType)
        {
            ResolveDefaults();
            ApplyNonBlocking();
            _currentMessage = message ?? string.Empty;

            if (_messageText != null)
            {
                _messageText.text = _currentMessage;
                _messageText.color = ResolveTextColor(toastType);
                _messageText.ForceMeshUpdate();
            }
            else
            {
                Debug.LogWarning("[UIToastFrame] TextMeshProUGUI target is missing.", this);
            }

            if (_backgroundGraphic != null)
            {
                _backgroundGraphic.color = ResolveBackgroundColor(toastType);
            }
        }

        public bool HasMessage(string message)
        {
            return string.Equals(_currentMessage, message ?? string.Empty, StringComparison.Ordinal);
        }

        public void Show(float duration, Action<UIToastFrame> onHidden)
        {
            CancelInternal();
            isHiding = false;
            _onHidden = onHidden;

            var transitionPlayer = GetTransitionPlayer();
            if (transitionPlayer != null)
            {
                transitionPlayer.Cancel();
                PlayTransition(isShow: true);
            }

            RestartLifetime(duration);
        }

        public void RefreshDuration(float duration)
        {
            if (isHiding)
            {
                return;
            }

            RestartLifetime(duration);
        }

        internal void ApplyGroupOffset(Vector2 offset)
        {
            CacheInitialLayout();

            rectTransform.anchorMin = _initialLayout.AnchorMin;
            rectTransform.anchorMax = _initialLayout.AnchorMax;
            rectTransform.pivot = _initialLayout.Pivot;
            rectTransform.sizeDelta = _initialLayout.SizeDelta;
            rectTransform.localScale = _initialLayout.LocalScale;
            rectTransform.anchoredPosition = _initialLayout.AnchoredPosition + offset;
        }

        private void RestartLifetime(float duration)
        {
            if (_lifetimeCoroutine != null)
            {
                StopCoroutine(_lifetimeCoroutine);
                _lifetimeCoroutine = null;
            }

            if (!isActiveAndEnabled)
            {
                return;
            }

            _lifetimeCoroutine = StartCoroutine(CoLifetime(Mathf.Max(0f, duration)));
        }

        private IEnumerator CoLifetime(float duration)
        {
            if (duration > 0f)
            {
                yield return new WaitForSecondsRealtime(duration);
            }

            HideInternal();
        }

        private void HideInternal()
        {
            if (isHiding)
            {
                return;
            }

            isHiding = true;

            if (_lifetimeCoroutine != null)
            {
                StopCoroutine(_lifetimeCoroutine);
                _lifetimeCoroutine = null;
            }

            if (GetTransitionPlayer() == null)
            {
                NotifyHidden();
                return;
            }

            var handle = PlayTransition(isShow: false, NotifyHidden);
            if (handle == null || handle.IsCanceled)
            {
                NotifyHidden();
            }
        }

        private void NotifyHidden()
        {
            var callback = _onHidden;
            _onHidden = null;
            callback?.Invoke(this);
        }

        private void CancelInternal()
        {
            if (_lifetimeCoroutine != null)
            {
                StopCoroutine(_lifetimeCoroutine);
                _lifetimeCoroutine = null;
            }

            var transitionPlayer = GetTransitionPlayer();
            if (transitionPlayer != null)
            {
                transitionPlayer.Cancel();
            }

            _onHidden = null;
        }

        private UITweenHandle PlayTransition(bool isShow, Action onComplete = null)
        {
            var transitionPlayer = GetTransitionPlayer();
            if (transitionPlayer == null)
            {
                return UITweenHandle.CreateCanceled();
            }

            var presetId = isShow ? _showTransitionId : _hideTransitionId;
            if (presetId == null || !presetId.IsValid)
            {
                return UITweenHandle.CreateCanceled();
            }

            return transitionPlayer.Play(presetId, onComplete);
        }

        private void ResolveDefaults()
        {
            if (_messageText == null)
            {
                _messageText = GetComponentInChildren<TextMeshProUGUI>(true);
            }

            if (_backgroundGraphic == null)
            {
                _backgroundGraphic = GetComponent<Image>();
                if (_backgroundGraphic == null)
                {
                    _backgroundGraphic = GetComponentInChildren<Image>(true);
                }
            }

            if (_canvasGroup == null)
            {
                _canvasGroup = GetComponentInChildren<CanvasGroup>(true);
            }
        }

        private UITransitionPlayer GetTransitionPlayer()
        {
            return GetComponent<UITransitionPlayer>();
        }

        private void CacheInitialLayout()
        {
            if (_hasInitialLayout || rectTransform == null)
            {
                return;
            }

            _initialLayout = new LayoutSnapshot
            {
                AnchorMin = rectTransform.anchorMin,
                AnchorMax = rectTransform.anchorMax,
                Pivot = rectTransform.pivot,
                AnchoredPosition = rectTransform.anchoredPosition,
                SizeDelta = rectTransform.sizeDelta,
                LocalScale = rectTransform.localScale
            };
            _hasInitialLayout = true;
        }

        private void ApplyNonBlocking()
        {
            if (_canvasGroup != null)
            {
                _canvasGroup.interactable = false;
                _canvasGroup.blocksRaycasts = false;
            }

            var graphics = GetComponentsInChildren<Graphic>(true);
            for (var i = 0; i < graphics.Length; i++)
            {
                graphics[i].raycastTarget = false;
            }
        }

        private Color ResolveBackgroundColor(ToastType type)
        {
            switch (type)
            {
                case ToastType.Success:
                    return _successBackgroundColor;
                case ToastType.Warning:
                    return _warningBackgroundColor;
                case ToastType.Error:
                    return _errorBackgroundColor;
                default:
                    return _infoBackgroundColor;
            }
        }

        private Color ResolveTextColor(ToastType type)
        {
            switch (type)
            {
                case ToastType.Success:
                    return _successTextColor;
                case ToastType.Warning:
                    return _warningTextColor;
                case ToastType.Error:
                    return _errorTextColor;
                default:
                    return _infoTextColor;
            }
        }
    }
}
