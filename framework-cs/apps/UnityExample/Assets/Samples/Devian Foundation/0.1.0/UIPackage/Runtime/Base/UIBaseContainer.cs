using System.Collections.Generic;
using UnityEngine;

namespace Devian
{
    /// <summary>
    /// UIBasePanel 수명주기에 통합되는 컨테이너의 기반 클래스.
    /// owner panel이 소유 subtree를 수집해 초기화한다.
    /// 초기화 순서: owned component.onInit() → owned frame.onInit() → container.onInit() → canvas.onInit()
    /// </summary>
    public abstract class UIBaseContainer : MonoBehaviour, IPoolable
    {
        private bool _isContainerInitComplete;
        private readonly List<UIBaseFrame> _ownedFrames = new List<UIBaseFrame>();

        /// <summary>
        /// 컨테이너가 초기화되었는지 여부.
        /// </summary>
        public bool isContainerInitialized { get; private set; }

        /// <summary>캐시된 RectTransform. edit mode에서는 Awake 전에 접근될 수 있어 lazy resolve한다.</summary>
        public RectTransform rectTransform => _rectTransform != null ? _rectTransform : _rectTransform = transform as RectTransform;

        /// <summary>
        /// 소유 Canvas. _Init(canvas) 호출 시 설정된다.
        /// </summary>
        public Canvas canvas { get; private set; }

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

        /// <summary>
        /// 컨테이너 초기화. owner panel이 호출한다.
        /// Canvas 참조를 저장하고 owned subtree를 먼저 초기화한 뒤 onInit()을 호출한다.
        /// 중복 호출 방지 (isContainerInitialized 가드).
        /// </summary>
        internal void _Init(Canvas canvas)
        {
            if (isContainerInitialized) return;
            this.canvas = canvas;
            _ownedFrames.Clear();
            UIBaseInitHelper.InitOwnedSubtree(transform, canvas, _ownedFrames);
            isContainerInitialized = true;
            onInit();
        }

        /// <summary>
        /// 모든 초기화가 완료된 후 호출.
        /// Container, Frame, Canvas의 Init이 모두 끝난 뒤 호출된다.
        /// </summary>
        internal void _InitComplete()
        {
            if (_isContainerInitComplete) return;
            _isContainerInitComplete = true;

            for (var i = 0; i < _ownedFrames.Count; i++)
                _ownedFrames[i]._InitComplete();

            onInitComplete();
        }

        internal void _ResetForPool()
        {
            UIBaseInitHelper.ResetOwnedSubtree(transform);
            _ownedFrames.Clear();
            isContainerInitialized = false;
            _isContainerInitComplete = false;
            canvas = null;
        }

        /// <summary>
        /// 컨테이너 초기화 로직. override하여 사용.
        /// 호출 시점: owned component/frame.onInit() 이후, Canvas.onInit() 이전.
        /// </summary>
        protected virtual void onInit() { }

        /// <summary>
        /// 모든 초기화가 완료된 후. override하여 사용.
        /// 호출 시점: Canvas.onInitComplete() 이전.
        /// </summary>
        protected virtual void onInitComplete() { }
        protected virtual void onPoolSpawned() { }
        protected virtual void onPoolDespawned() { }
        protected virtual void onDestroy() { }

        // ─── IPoolable ───

        public void OnPoolSpawned()
        {
            onPoolSpawned();
        }

        public void OnPoolDespawned()
        {
            _HandlePoolDespawnedRecursive();
        }

        internal void _HandlePoolDespawnedRecursive()
        {
            onPoolDespawned();
            _ResetForPool();
        }

        private RectTransform _rectTransform;
    }
}
