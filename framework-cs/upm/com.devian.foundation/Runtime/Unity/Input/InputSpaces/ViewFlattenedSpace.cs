using UnityEngine;

namespace Devian
{
    /// <summary>
    /// 카메라 forward/right를 y=0 평탄화 후 합성.
    /// 3인칭 / 쿼터뷰 등 카메라 기준 이동에 사용.
    /// </summary>
    public class ViewFlattenedSpace : IInputSpace
    {
        private readonly Transform _cameraTransform;

        public ViewFlattenedSpace(Transform cameraTransform)
        {
            _cameraTransform = cameraTransform;
        }

        public Vector3 ResolveMove(Vector2 raw)
        {
            Vector3 forward = _cameraTransform.forward;
            Vector3 right   = _cameraTransform.right;

            // y=0 평탄화
            forward.y = 0f;
            right.y   = 0f;

            forward.Normalize();
            right.Normalize();

            return right * raw.x + forward * raw.y;
        }
    }
}
