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
    /// Init phase tracking for UICanvas.
    /// </summary>
    internal enum UICanvasInitPhase
    {
        None,
        ContainerInit,
        PanelInit,
        CanvasInit,
        ContainerInitComplete,
        PanelInitComplete,
        CanvasInitComplete,
        Completed
    }

    /// <summary>
    /// 비제네릭 UICanvas 베이스.
    /// Canvas 캐시, Init 전체 흐름, Validate, 동적 container 등록을 담당한다.
    /// UIPanel은 ownerBase as UICanvas로 공통 정보에 접근한다.
    /// </summary>
    public abstract class UICanvas : MonoBehaviour
    {
        /// <summary>The Unity Canvas component cached on Awake.</summary>
        public Canvas canvas { get; private set; }

        /// <summary>Init()이 호출되었는지.</summary>
        public bool isInitialized { get; private set; }

        /// <summary>Init() 전체가 완료되었는지 (Phase 7 이후).</summary>
        public bool isInitComplete { get; private set; }

        internal UICanvasInitPhase _currentPhase = UICanvasInitPhase.None;

        List<UIPanel> mPanels = new List<UIPanel>();
        List<UIBaseContainer> mContainers = new List<UIBaseContainer>();
        List<UIBaseContainer> mPendingDynamicContainers = new List<UIBaseContainer>();

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
        /// Phase 1: Container._Init(canvas)
        /// Phase 2: Panel._InitFromCanvas(owner)
        /// Phase 3: Canvas.onInit()
        /// Phase 4: Container._InitComplete() + pending dynamic flush
        /// Phase 5: Panel._InitComplete()
        /// Phase 6: Canvas.onInitComplete()
        /// Phase 7: Notify(InitOnce)
        /// </summary>
        public void Init()
        {
            if (isInitialized) return;
            isInitialized = true;

            // 수집
            mContainers.AddRange(GetComponentsInChildren<UIBaseContainer>(true));
            mPanels.AddRange(GetComponentsInChildren<UIPanel>(true));

            // Phase 1: Container Init
            _currentPhase = UICanvasInitPhase.ContainerInit;
            for (int i = 0; i < mContainers.Count; i++)
                mContainers[i]._Init(canvas);

            // Phase 2: Panel Init
            _currentPhase = UICanvasInitPhase.PanelInit;
            for (int i = 0; i < mPanels.Count; i++)
                mPanels[i]._InitFromCanvas(this);

            // Phase 3: Canvas Init
            _currentPhase = UICanvasInitPhase.CanvasInit;
            onInit();

            // Phase 4: Container InitComplete + pending dynamic flush
            _currentPhase = UICanvasInitPhase.ContainerInitComplete;
            for (int i = 0; i < mContainers.Count; i++)
                mContainers[i]._InitComplete();

            FlushPendingDynamicContainers();

            // Phase 5: Panel InitComplete
            _currentPhase = UICanvasInitPhase.PanelInitComplete;
            for (int i = 0; i < mPanels.Count; i++)
                mPanels[i]._InitComplete();

            // Phase 6: Canvas InitComplete
            _currentPhase = UICanvasInitPhase.CanvasInitComplete;
            onInitComplete();

            // Phase 7: Notify
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
    /// 타입 안전 UICanvas. 씬 종속 싱글톤.
    /// </summary>
    public abstract class UICanvas<TCanvas> : UICanvas
        where TCanvas : UICanvas
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
            if (!BaseApplication.IsApplicationQuitting)
                onDestroy();
            if (Instance == (this as TCanvas))
                Instance = null;
        }
    }
}
