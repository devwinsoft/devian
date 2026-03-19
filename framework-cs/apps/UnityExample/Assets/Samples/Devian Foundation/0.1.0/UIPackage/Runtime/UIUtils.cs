using UnityEngine;

namespace Devian
{
    /// <summary>
    /// UIPackage 공용 유틸리티 함수.
    /// 인스턴스 상태에 의존하지 않는 순수 함수만 포함한다.
    /// </summary>
    public static class UIUtils
    {
        // ────────────────────────────────────────────
        // Coordinate Conversion
        // ────────────────────────────────────────────

        /// <summary>
        /// World 좌표를 Overlay UI의 로컬 좌표로 변환한다.
        /// </summary>
        /// <param name="worldCamera">월드 카메라 (canvas.worldCamera).</param>
        /// <param name="overlaySpace">변환 대상 RectTransform.</param>
        /// <param name="worldPos">월드 좌표.</param>
        /// <param name="overlayLocal">변환된 로컬 좌표.</param>
        /// <returns>변환 성공 여부.</returns>
        public static bool TryWorldToOverlayLocal(
            Camera worldCamera,
            RectTransform overlaySpace,
            Vector3 worldPos,
            out Vector2 overlayLocal)
        {
            overlayLocal = Vector2.zero;

            if (worldCamera == null)
            {
                return false;
            }

            Vector3 screenPos = worldCamera.WorldToScreenPoint(worldPos);

            if (screenPos.z < 0)
            {
                return false;
            }

            Camera eventCam = null;
            Canvas rootCanvas = overlaySpace.GetComponentInParent<Canvas>()?.rootCanvas;
            if (rootCanvas != null && rootCanvas.renderMode != RenderMode.ScreenSpaceOverlay)
            {
                eventCam = rootCanvas.worldCamera;
            }

            return RectTransformUtility.ScreenPointToLocalPointInRectangle(
                overlaySpace,
                screenPos,
                eventCam,
                out overlayLocal);
        }

        // ────────────────────────────────────────────
        // Billboard
        // ────────────────────────────────────────────

        /// <summary>
        /// 카메라를 향하는 billboard 회전을 계산한다.
        /// </summary>
        /// <param name="camera">기준 카메라.</param>
        /// <param name="targetWorldPos">회전 대상의 월드 위치.</param>
        /// <param name="mode">Billboard 모드.</param>
        /// <returns>카메라를 향하는 회전값.</returns>
        public static Quaternion ComputeBillboardRotation(
            Camera camera,
            Vector3 targetWorldPos,
            BillboardMode mode = BillboardMode.Full)
        {
            if (camera == null)
            {
                return Quaternion.identity;
            }

            Vector3 camPos = camera.transform.position;
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
        /// target Transform에 billboard 회전을 적용한다.
        /// </summary>
        /// <param name="camera">기준 카메라.</param>
        /// <param name="target">회전을 적용할 Transform.</param>
        /// <param name="mode">Billboard 모드.</param>
        public static void ApplyBillboard(
            Camera camera,
            Transform target,
            BillboardMode mode = BillboardMode.Full)
        {
            target.rotation = ComputeBillboardRotation(camera, target.position, mode);
        }

        // ────────────────────────────────────────────
        // Cursor
        // ────────────────────────────────────────────

        /// <summary>
        /// 커서 가시성 및 잠금 모드를 설정한다.
        /// </summary>
        /// <param name="visible">커서 표시 여부.</param>
        /// <param name="lockMode">커서 잠금 모드.</param>
        public static void SetCursor(bool visible, CursorLockMode lockMode)
        {
            Cursor.visible = visible;
            Cursor.lockState = lockMode;
        }
        // ────────────────────────────────────────────
        // RectTransform Size
        // ────────────────────────────────────────────

        /// <summary>
        /// RectTransform의 실제 렌더링 폭을 반환한다.
        /// anchor/pivot에 관계없이 레이아웃 후 정확한 값.
        /// </summary>
        public static float GetWidth(RectTransform rt) => rt.rect.width;

        /// <summary>
        /// RectTransform의 실제 렌더링 높이를 반환한다.
        /// anchor/pivot에 관계없이 레이아웃 후 정확한 값.
        /// </summary>
        public static float GetHeight(RectTransform rt) => rt.rect.height;

        /// <summary>
        /// RectTransform의 실제 렌더링 크기를 반환한다.
        /// </summary>
        public static Vector2 GetSize(RectTransform rt) => rt.rect.size;
    }
}
