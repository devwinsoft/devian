using System.Collections.Generic;
using UnityEngine;

namespace Devian
{
    /// <summary>
    /// UIBaseContainer 내부의 하위 요소(Frame/Grid) 기반 클래스.
    /// owner panel / container / frame이 owned subtree 규칙으로 초기화한다.
    /// Scroll 정보를 소유하지 않는다.
    /// Scroll 전용 계약은 IUIScrollSection 인터페이스로 분리된다.
    /// </summary>
    public abstract class UIBaseFrame : MonoBehaviour
    {
        // ─── Lifecycle ───
        private readonly List<UIBaseFrame> _ownedFrames = new List<UIBaseFrame>();

        public bool isFrameInitialized { get; private set; }
        public bool isFrameInitComplete { get; private set; }
        public Canvas canvas { get; private set; }

        /// <summary>캐시된 RectTransform. edit mode에서는 Awake 전에 접근될 수 있어 lazy resolve한다.</summary>
        public RectTransform rectTransform => _rectTransform != null ? _rectTransform : _rectTransform = transform as RectTransform;

        protected void Awake()
        {
            _rectTransform = transform as RectTransform;
            onAwake();
        }

        /// <summary>Override this for custom Awake logic.</summary>
        protected virtual void onAwake() { }

        protected void OnDestroy()
        {
            if (Application.isPlaying
                && !BaseApplication.IsShuttingDown
                && !BaseApplication.IsApplicationQuitting)
                onDestroy();
        }

        internal void _Init(Canvas canvas)
        {
            if (isFrameInitialized) return;
            this.canvas = canvas;
            _ownedFrames.Clear();
            UIBaseInitHelper.InitOwnedSubtree(transform, canvas, _ownedFrames, this);
            isFrameInitialized = true;
            onInit();
        }

        internal void _InitComplete()
        {
            if (isFrameInitComplete) return;
            isFrameInitComplete = true;

            for (var i = 0; i < _ownedFrames.Count; i++)
                _ownedFrames[i]._InitComplete();

            onInitComplete();
        }

        internal void _ResetForPool()
        {
            UIBaseInitHelper.ResetOwnedSubtree(transform, this);
            _ownedFrames.Clear();
            isFrameInitialized = false;
            isFrameInitComplete = false;
            canvas = null;
        }

        protected void _HandlePoolSpawned()
        {
            onPoolSpawned();
        }

        protected void _HandlePoolDespawned()
        {
            _HandlePoolDespawnedRecursive();
        }

        internal void _HandlePoolDespawnedRecursive()
        {
            onPoolDespawned();
            _ResetForPool();
        }

        protected virtual void onInit() { }
        protected virtual void onInitComplete() { }
        protected virtual void onPoolSpawned() { }
        protected virtual void onPoolDespawned() { }
        protected virtual void onDestroy() { }

        // ─── Size ───

        public virtual float GetWidth() => UIUtils.GetWidth(rectTransform);
        public virtual float GetHeight() => UIUtils.GetHeight(rectTransform);

        // ─── Clear ───

        internal virtual void _Clear() { }

        private RectTransform _rectTransform;
    }
}
