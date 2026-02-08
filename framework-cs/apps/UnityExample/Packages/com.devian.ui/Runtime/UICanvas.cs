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
    /// Inherits from CompoSingleton for scene-placed singleton pattern.
    /// Frames are initialized when Init() is called.
    /// </summary>
    /// <typeparam name="TCanvas">The derived canvas type.</typeparam>
    public abstract class UICanvas<TCanvas> : CompoSingleton<TCanvas>
        where TCanvas : MonoBehaviour
    {
        /// <summary>
        /// The Unity Canvas component cached on Awake.
        /// </summary>
        public Canvas canvas { get; private set; }

        bool mInitialized = false;
        List<BaseUIFrame> mFrames = new List<BaseUIFrame>();

        /// <summary>
        /// Unity Awake callback. Overrides CompoSingleton.Awake().
        /// Use onAwake for custom initialization in derived classes.
        /// </summary>
        protected override void Awake()
        {
            base.Awake();
            canvas = GetComponent<Canvas>();
            onAwake();
        }

        /// <summary>
        /// Override this for custom initialization logic.
        /// Called after canvas is cached. Frame initialization happens in Init().
        /// </summary>
        protected virtual void onAwake() { }

        protected virtual void onInit() { }
        protected virtual void onInitComplete() { }

        public void Init()
        {
            if (mInitialized) return;
            mInitialized = true;
            mFrames.AddRange(GetComponentsInChildren<BaseUIFrame>(true));
            onInit();
            foreach (var frame in mFrames)
            {
                frame._InitFromCanvas(this);
            }
            onInitComplete();
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
            where FRAME : Component, IPoolable<FRAME>
        {
            var instance = BundlePool.Spawn<FRAME>(
                prefabName,
                parent: parent ?? transform);

            var frameBase = instance.GetComponent<BaseUIFrame>();
            if (frameBase != null && mInitialized)
            {
                mFrames.Add(frameBase);
                frameBase._InitFromCanvas(this);
            }

            return instance;
        }

        /// <summary>
        /// Converts a world position to local position in overlay space.
        /// </summary>
        /// <param name="overlaySpace">The RectTransform in overlay space.</param>
        /// <param name="worldPos">World position to convert.</param>
        /// <param name="overlayLocal">Output local position in overlay space.</param>
        /// <returns>True if conversion succeeded, false otherwise.</returns>
        public bool TryWorldToOverlayLocal(RectTransform overlaySpace, Vector3 worldPos, out Vector2 overlayLocal)
        {
            overlayLocal = Vector2.zero;

            if (canvas.worldCamera == null)
            {
                return false;
            }

            // Project world position to screen
            Vector3 screenPos = canvas.worldCamera.WorldToScreenPoint(worldPos);

            // Check if position is behind camera
            if (screenPos.z < 0)
            {
                return false;
            }

            // Determine event camera based on overlay space's root canvas render mode
            Camera eventCam = null;
            Canvas rootCanvas = overlaySpace.GetComponentInParent<Canvas>()?.rootCanvas;
            if (rootCanvas != null && rootCanvas.renderMode != RenderMode.ScreenSpaceOverlay)
            {
                eventCam = rootCanvas.worldCamera;
            }

            // Convert screen position to local position in overlay space
            return RectTransformUtility.ScreenPointToLocalPointInRectangle(
                overlaySpace,
                screenPos,
                eventCam,
                out overlayLocal);
        }

        /// <summary>
        /// Computes billboard rotation to face the camera.
        /// </summary>
        /// <param name="targetWorldPos">World position of the object to billboard.</param>
        /// <param name="mode">Billboard rotation mode.</param>
        /// <returns>Quaternion rotation facing the camera.</returns>
        public Quaternion ComputeBillboardRotation(Vector3 targetWorldPos, BillboardMode mode = BillboardMode.Full)
        {
            if (canvas.worldCamera == null)
            {
                return Quaternion.identity;
            }

            Vector3 camPos = canvas.worldCamera.transform.position;
            Vector3 dirToCamera = camPos - targetWorldPos;

            if (dirToCamera.sqrMagnitude < 0.0001f)
            {
                return Quaternion.identity;
            }

            if (mode == BillboardMode.YOnly)
            {
                dirToCamera.y = 0f;
                if (dirToCamera.sqrMagnitude < 0.0001f)
                {
                    return Quaternion.identity;
                }
            }

            return Quaternion.LookRotation(dirToCamera);
        }

        /// <summary>
        /// Applies billboard rotation to a target transform.
        /// </summary>
        /// <param name="target">Transform to apply billboard rotation to.</param>
        /// <param name="mode">Billboard rotation mode.</param>
        public void ApplyBillboard(Transform target, BillboardMode mode = BillboardMode.Full)
        {
            target.rotation = ComputeBillboardRotation(target.position, mode);
        }
    }
}
