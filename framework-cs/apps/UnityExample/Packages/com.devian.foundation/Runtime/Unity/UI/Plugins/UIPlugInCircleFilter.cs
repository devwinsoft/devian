using UnityEngine;
using UnityEngine.UI;

namespace Devian
{
    /// <summary>
    /// Circle/Collider2D-based raycast filter for UI elements.
    /// Filters raycasts based on whether the point is inside the Collider2D.
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    [RequireComponent(typeof(RectTransform))]
    public class UIPlugInCircleFilter : MonoBehaviour, ICanvasRaycastFilter
    {
        private Collider2D _collider;
        private RectTransform _rectTransform;

        private void Awake()
        {
            _collider = GetComponent<Collider2D>();
            _rectTransform = GetComponent<RectTransform>();
        }

        public bool IsRaycastLocationValid(Vector2 screenPoint, Camera eventCamera)
        {
            if (_collider == null || _rectTransform == null)
            {
                return false;
            }

            Vector3 worldPoint;
            if (!RectTransformUtility.ScreenPointToWorldPointInRectangle(
                _rectTransform,
                screenPoint,
                eventCamera,
                out worldPoint))
            {
                return false;
            }

            return _collider.OverlapPoint(worldPoint);
        }
    }
}
