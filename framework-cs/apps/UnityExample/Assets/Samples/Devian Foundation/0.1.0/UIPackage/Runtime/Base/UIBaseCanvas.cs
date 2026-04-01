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
        PanelInit,
        CanvasInit,
        PanelInitComplete,
        CanvasInitComplete,
        Completed
    }

    /// <summary>
    /// 비제네릭 UIBaseCanvas 베이스.
    /// Canvas 캐시, Init 전체 흐름, Validate를 담당한다.
    /// UIBasePanel은 ownerBase as UIBaseCanvas로 공통 정보에 접근한다.
    /// </summary>
    public abstract class UIBaseCanvas : MonoBehaviour
    {
        /// <summary>The Unity Canvas component cached on Awake.</summary>
        public Canvas canvas { get; private set; }

        /// <summary>Init()이 호출되었는지.</summary>
        public bool isInitialized { get; private set; }

        /// <summary>Init() 전체가 완료되었는지 (Notify 이후).</summary>
        public bool isInitComplete { get; private set; }

        internal UICanvasInitPhase _currentPhase = UICanvasInitPhase.None;

        List<UIBasePanel> mPanels = new List<UIBasePanel>();

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
        protected virtual void onPoolSpawned() { }
        protected virtual void onPoolDespawned() { }

        // ─── Init ───

        /// <summary>
        /// 초기화 순서:
        /// Phase 1: canvas-owned UIComponentBase init
        /// Phase 2: Panel._InitFromCanvas(owner)
        /// Phase 3: Canvas.onInit()
        /// Phase 4: Panel._InitComplete()
        /// Phase 5: Canvas.onInitComplete()
        /// Phase 6: Notify(InitOnce)
        /// </summary>
        public void Init()
        {
            if (isInitialized) return;
            isInitialized = true;

            // 수집
            mPanels.AddRange(GetComponentsInChildren<UIBasePanel>(true));

            // Phase 1: Canvas-owned component init
            _currentPhase = UICanvasInitPhase.ComponentInit;
            UIBaseInitHelper.InitCanvasOwnedSubtree(transform, canvas);

            // Phase 2: Panel Init
            _currentPhase = UICanvasInitPhase.PanelInit;
            for (int i = 0; i < mPanels.Count; i++)
                mPanels[i]._InitFromCanvas(this);

            // Phase 3: Canvas Init
            _currentPhase = UICanvasInitPhase.CanvasInit;
            onInit();

            // Phase 4: Panel InitComplete
            _currentPhase = UICanvasInitPhase.PanelInitComplete;
            for (int i = 0; i < mPanels.Count; i++)
                mPanels[i]._InitComplete();

            // Phase 5: Canvas InitComplete
            _currentPhase = UICanvasInitPhase.CanvasInitComplete;
            onInitComplete();

            // Phase 6: Notify
            _currentPhase = UICanvasInitPhase.Completed;
            isInitComplete = true;
            UIManager.messageSystem.Notify(UI_MESSAGE.InitOnce);
        }

        protected void _HandlePoolSpawned()
        {
            onPoolSpawned();

            if (isInitialized)
            {
                UIBaseInitHelper.InitCanvasOwnedSubtree(transform, canvas);

                var panels = GetComponentsInChildren<UIBasePanel>(true);
                for (var i = 0; i < panels.Length; i++)
                    panels[i]._HandleOwnerPoolSpawned();
            }
        }

        protected void _HandlePoolDespawned()
        {
            onPoolDespawned();

            if (isInitialized)
            {
                var panels = GetComponentsInChildren<UIBasePanel>(true);
                for (var i = 0; i < panels.Length; i++)
                    panels[i]._HandleOwnerPoolDespawned();

                UIBaseInitHelper.ResetCanvasOwnedSubtree(transform);
            }
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
