using UnityEngine;
using System.Collections.Generic;

namespace Devian
{
    /// <summary>
    /// Billboard rotation mode for UI elements facing the camera.
    /// </summary>
    public enum BillboardMode
    {
        /// <summary>Full rotation to face camera on all axes.</summary>
        Full,
        /// <summary>Rotation only around Y axis (vertical billboard).</summary>
        YOnly
    }

    /// <summary>
    /// Base class for UI Canvas owners.
    /// 씬 종속 MonoBehaviour 싱글톤. DontDestroyOnLoad 미적용 — 씬 전환 시 자동 파괴.
    /// Init() 호출 시 Container → Frame → Canvas 순서로 초기화한다.
    /// </summary>
    /// <typeparam name="TCanvas">The derived canvas type.</typeparam>
    public abstract class UICanvas<TCanvas> : MonoBehaviour
        where TCanvas : MonoBehaviour
    {
        /// <summary>
        /// 씬 종속 싱글톤 인스턴스. Awake에서 설정, OnDestroy에서 클린업.
        /// </summary>
        public static TCanvas Instance { get; private set; }

        /// <summary>
        /// The Unity Canvas component cached on Awake.
        /// </summary>
        public Canvas canvas { get; private set; }

        bool mInitialized = false;
        List<UICanvasFrame> mFrames = new List<UICanvasFrame>();
        List<UIContainerBase> mContainers = new List<UIContainerBase>();

        /// <summary>
        /// Unity Awake callback. Instance 설정 + canvas 캐시 + onAwake().
        /// non-virtual — 파생 클래스는 onAwake()를 override하여 사용한다.
        /// </summary>
        protected void Awake()
        {
            Instance = this as TCanvas;
            canvas = GetComponent<Canvas>();
            onAwake();
        }

        /// <summary>
        /// Unity OnDestroy callback. onDestroy() 호출 + Instance 클린업.
        /// non-virtual — 파생 클래스는 onDestroy()를 override하여 사용한다.
        /// IsApplicationQuitting == true이면 onDestroy()를 호출하지 않는다.
        /// </summary>
        protected void OnDestroy()
        {
            if (!BaseApplication.IsApplicationQuitting)
                onDestroy();
            if (Instance == (this as TCanvas))
                Instance = null;
        }

        /// <summary>
        /// Override this for custom initialization logic.
        /// Called after canvas is cached. Frame initialization happens in Init().
        /// </summary>
        protected virtual void onAwake() { }

        /// <summary>
        /// Override this for custom cleanup logic.
        /// Called before Instance cleanup in OnDestroy.
        /// </summary>
        protected virtual void onDestroy() { }

        /// <summary>
        /// Canvas 초기화. Container → Frame → Canvas 이후 호출.
        /// </summary>
        protected virtual void onInit() { }

        /// <summary>
        /// 모든 초기화 완료 후 호출. Container.onInitComplete → Frame.onInitComplete 이후 호출.
        /// </summary>
        protected virtual void onInitComplete() { }

        /// <summary>
        /// 초기화 순서:
        /// 1. Container 수집 + Container._Init()   (Container.onInit)
        /// 2. Frame 수집 + Frame._InitFromCanvas()  (Frame.onInit)
        /// 3. Canvas.onInit()
        /// 4. Container._InitComplete()  (Container.onInitComplete)
        /// 5. Frame._InitComplete()   (Frame.onInitComplete)
        /// 6. Canvas.onInitComplete()
        /// 7. Notify(InitOnce)
        /// </summary>
        public void Init()
        {
            if (mInitialized) return;
            mInitialized = true;

            // 수집
            mContainers.AddRange(GetComponentsInChildren<UIContainerBase>(true));
            mFrames.AddRange(GetComponentsInChildren<UICanvasFrame>(true));

            // Phase 1: Container Init (최하위)
            foreach (var comp in mContainers)
            {
                comp._Init(canvas);
            }

            // Phase 2: Frame Init
            foreach (var frame in mFrames)
            {
                frame._InitFromCanvas(this);
            }

            // Phase 3: Canvas Init
            onInit();

            // Phase 4: InitComplete (Container → Frame → Canvas)
            foreach (var comp in mContainers)
            {
                comp._InitComplete();
            }

            foreach (var frame in mFrames)
            {
                frame._InitComplete();
            }

            onInitComplete();

            // Phase 5: Notify
            UIManager.messageSystem.Notify(UI_MESSAGE.InitOnce);
        }

        /// <summary>
        /// Validates the canvas configuration.
        /// </summary>
        /// <param name="reason">Output reason if validation fails.</param>
        /// <returns>True if valid, false otherwise.</returns>
        public virtual bool Validate(out string reason)
        {
            if (canvas == null)
            {
                reason = "Canvas component not found";
                return false;
            }

            // ScreenSpaceOverlay should not have worldCamera
            if (canvas.renderMode == RenderMode.ScreenSpaceOverlay && canvas.worldCamera != null)
            {
                reason = "ScreenSpaceOverlay should not have worldCamera assigned";
                return false;
            }

            // ScreenSpaceCamera must have worldCamera
            if (canvas.renderMode == RenderMode.ScreenSpaceCamera && canvas.worldCamera == null)
            {
                reason = "ScreenSpaceCamera requires worldCamera to be assigned";
                return false;
            }

            reason = null;
            return true;
        }

        /// <summary>
        /// Creates a new frame instance using BundlePool.
        /// When initialized, the created frame is added to the frame list and _InitFromCanvas(this) is called.
        /// </summary>
        /// <typeparam name="FRAME">The frame component type. Must implement IPoolable.</typeparam>
        /// <param name="prefabName">Name of the prefab in the bundle.</param>
        /// <param name="parent">Parent transform. Defaults to this frame's transform if null.</param>
        /// <returns>The created and initialized frame instance.</returns>
        public FRAME CreateFrame<FRAME>(string prefabName, Transform parent = null)
            where FRAME : Component, IPoolable
        {
            var instance = BundlePool.Spawn<FRAME>(
                prefabName,
                parent: parent ?? transform);

            var frameBase = instance.GetComponent<UICanvasFrame>();
            if (frameBase != null && mInitialized)
            {
                mFrames.Add(frameBase);
                frameBase._InitFromCanvas(this);
            }

            return instance;
        }

    }
}
