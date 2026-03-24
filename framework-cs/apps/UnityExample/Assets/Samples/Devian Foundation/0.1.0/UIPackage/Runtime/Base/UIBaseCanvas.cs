using UnityEngine;
using System.Collections.Generic;

namespace Devian
{
    /// <summary>
    /// Billboard rotation mode for UI elements facing the camera.
    /// </summary>
    public enum BillboardMode
    {
        Full,
        YOnly
    }

    /// <summary>
    /// Init phase tracking for UIBaseCanvas.
    /// </summary>
    internal enum UICanvasInitPhase
    {
        None,
        ComponentInit,
        ContainerInit,
        PanelInit,
        CanvasInit,
        ContainerInitComplete,
        PanelInitComplete,
        CanvasInitComplete,
        Completed
    }

    /// <summary>
    /// 비제네릭 UIBaseCanvas 베이스.
    /// Canvas 캐시, Init 전체 흐름, Validate, 동적 container/frame 등록을 담당한다.
    /// UIBasePanel은 ownerBase as UIBaseCanvas로 공통 정보에 접근한다.
    /// </summary>
    public abstract class UIBaseCanvas : MonoBehaviour
    {
        /// <summary>The Unity Canvas component cached on Awake.</summary>
        public Canvas canvas { get; private set; }

        /// <summary>Init()이 호출되었는지.</summary>
        public bool isInitialized { get; private set; }

        /// <summary>Init() 전체가 완료되었는지 (Phase 8 이후).</summary>
        public bool isInitComplete { get; private set; }

        internal UICanvasInitPhase _currentPhase = UICanvasInitPhase.None;

        List<UIBasePanel> mPanels = new List<UIBasePanel>();
        List<UIBaseContainer> mContainers = new List<UIBaseContainer>();
        List<UIBaseContainer> mPendingDynamicContainers = new List<UIBaseContainer>();
        List<UIBaseFrame> mPendingDynamicFrames = new List<UIBaseFrame>();

        // ─── Awake / OnDestroy (subclass에서 sealed) ───

        protected void _CacheCanvas()
        {
            canvas = GetComponent<Canvas>();
        }

        // ─── Virtual hooks ───

        protected virtual void onAwake() { }
        protected virtual void onDestroy() { }
        protected virtual void onInit() { }
        protected virtual void onInitComplete() { }

        // ─── Init ───

        /// <summary>
        /// 초기화 순서:
        /// Phase 1: canvas-owned UIComponentBase / UIBaseFrame init
        /// Phase 2: Container._Init(canvas)
        /// Phase 3: Panel._InitFromCanvas(owner)
        /// Phase 4: Canvas.onInit()
        /// Phase 5: Container._InitComplete() + pending dynamic flush
        /// Phase 6: Panel._InitComplete()
        /// Phase 7: Canvas.onInitComplete()
        /// Phase 8: Notify(InitOnce)
        /// </summary>
        public void Init()
        {
            if (isInitialized) return;
            isInitialized = true;

            // 수집
            mContainers.AddRange(GetComponentsInChildren<UIBaseContainer>(true));
            mPanels.AddRange(GetComponentsInChildren<UIBasePanel>(true));

            // Phase 1: Canvas-owned component/frame init
            _currentPhase = UICanvasInitPhase.ComponentInit;
            UIBaseInitHelper.InitOwnedSubtree(transform, canvas);

            // Phase 2: Container Init
            _currentPhase = UICanvasInitPhase.ContainerInit;
            for (int i = 0; i < mContainers.Count; i++)
                mContainers[i]._Init(canvas);

            // Phase 3: Panel Init
            _currentPhase = UICanvasInitPhase.PanelInit;
            for (int i = 0; i < mPanels.Count; i++)
                mPanels[i]._InitFromCanvas(this);

            // Phase 4: Canvas Init
            _currentPhase = UICanvasInitPhase.CanvasInit;
            onInit();

            // Phase 5: Container InitComplete + pending dynamic flush
            _currentPhase = UICanvasInitPhase.ContainerInitComplete;
            for (int i = 0; i < mContainers.Count; i++)
                mContainers[i]._InitComplete();

            FlushPendingDynamicContainers();

            // Phase 6: Panel InitComplete
            _currentPhase = UICanvasInitPhase.PanelInitComplete;
            FlushPendingDynamicFrames();
            for (int i = 0; i < mPanels.Count; i++)
                mPanels[i]._InitComplete();

            // Phase 7: Canvas InitComplete
            _currentPhase = UICanvasInitPhase.CanvasInitComplete;
            onInitComplete();

            // Phase 8: Notify
            _currentPhase = UICanvasInitPhase.Completed;
            isInitComplete = true;
            UIManager.messageSystem.Notify(UI_MESSAGE.InitOnce);
        }

        // ─── Dynamic Container Registration ───

        /// <summary>
        /// 동적으로 생성된 container subtree를 현재 canvas lifecycle에 편입한다.
        /// Init 진행 중이면 pending queue에 등록, Init 완료 후면 즉시 초기화.
        /// </summary>
        internal void RegisterDynamicContainerTree(UIBaseContainer root)
        {
            // root 이하 모든 UIBaseContainer 수집
            var containers = root.GetComponentsInChildren<UIBaseContainer>(true);

            foreach (var c in containers)
            {
                if (c.isContainerInitialized) continue;
                c._Init(canvas);
                mContainers.Add(c);

                if (isInitComplete)
                {
                    // Init 완료 후: 즉시 InitComplete
                    c._InitComplete();
                }
                else if (_currentPhase < UICanvasInitPhase.ContainerInitComplete)
                {
                    // Init 진행 중, Phase 4 이전: pending queue에 등록
                    mPendingDynamicContainers.Add(c);
                }
                else
                {
                    // Init 진행 중, Phase 4 이후: 즉시 InitComplete
                    c._InitComplete();
                }
            }
        }

        private void FlushPendingDynamicContainers()
        {
            if (mPendingDynamicContainers.Count == 0) return;

            foreach (var c in mPendingDynamicContainers)
            {
                if (!c.isContainerInitialized) continue;
                c._InitComplete();
            }
            mPendingDynamicContainers.Clear();
        }

        // ─── Dynamic Frame Registration ───

        /// <summary>
        /// 동적으로 생성된 frame subtree를 현재 canvas lifecycle에 편입한다.
        /// Init 진행 중이면 panel init complete 이전까지 pending queue에 등록한다.
        /// </summary>
        internal void RegisterDynamicFrameTree(UIBaseFrame root)
        {
            if (root == null) return;

            var frames = root.GetComponentsInChildren<UIBaseFrame>(true);

            foreach (var frame in frames)
            {
                if (!frame.isFrameInitialized)
                    frame._Init(canvas);

                if (frame.isFrameInitComplete)
                    continue;

                if (isInitComplete)
                {
                    frame._InitComplete();
                }
                else if (_currentPhase < UICanvasInitPhase.PanelInitComplete)
                {
                    if (!mPendingDynamicFrames.Contains(frame))
                        mPendingDynamicFrames.Add(frame);
                }
                else
                {
                    frame._InitComplete();
                }
            }
        }

        private void FlushPendingDynamicFrames()
        {
            if (mPendingDynamicFrames.Count == 0) return;

            foreach (var frame in mPendingDynamicFrames)
            {
                if (!frame.isFrameInitialized || frame.isFrameInitComplete) continue;
                frame._InitComplete();
            }
            mPendingDynamicFrames.Clear();
        }

        // ─── Validate ───

        public virtual bool Validate(out string reason)
        {
            if (canvas == null)
            {
                reason = "Canvas component not found";
                return false;
            }

            if (canvas.renderMode == RenderMode.ScreenSpaceOverlay && canvas.worldCamera != null)
            {
                reason = "ScreenSpaceOverlay should not have worldCamera assigned";
                return false;
            }

            if (canvas.renderMode == RenderMode.ScreenSpaceCamera && canvas.worldCamera == null)
            {
                reason = "ScreenSpaceCamera requires worldCamera to be assigned";
                return false;
            }

            reason = null;
            return true;
        }
    }

    /// <summary>
    /// 타입 안전 UIBaseCanvas. 씬 종속 싱글톤.
    /// </summary>
    public abstract class UIBaseCanvas<TCanvas> : UIBaseCanvas
        where TCanvas : UIBaseCanvas
    {
        /// <summary>씬 종속 싱글톤 인스턴스.</summary>
        public static TCanvas Instance { get; private set; }

        protected void Awake()
        {
            Instance = this as TCanvas;
            _CacheCanvas();
            onAwake();
        }

        protected void OnDestroy()
        {
            if (Application.isPlaying
                && !BaseApplication.IsShuttingDown
                && !BaseApplication.IsApplicationQuitting)
                onDestroy();
            if (Instance == (this as TCanvas))
                Instance = null;
        }
    }
}
