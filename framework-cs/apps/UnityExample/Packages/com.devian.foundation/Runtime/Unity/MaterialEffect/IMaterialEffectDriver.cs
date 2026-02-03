using UnityEngine;

namespace Devian
{
    /// <summary>
    /// 단일 Renderer를 위한 Material 교체 Driver 인터페이스 (v2).
    /// PropertyBlock/Common 관련 API는 제거됨.
    /// </summary>
    public interface IMaterialEffectDriver
    {
        /// <summary>
        /// Driver가 유효한지 여부 (Renderer가 존재하고 사용 가능한 상태).
        /// </summary>
        bool IsValid { get; }

        /// <summary>
        /// 현재 Renderer의 sharedMaterials를 clone하여 baseline으로 저장한다.
        /// </summary>
        void CaptureBaseline();

        /// <summary>
        /// baseline으로 저장된 Material[]을 Renderer에 복원한다.
        /// apply-clone도 함께 정리된다.
        /// </summary>
        void RestoreBaseline();

        /// <summary>
        /// baseline clone들을 Destroy하고 정리한다.
        /// OnDestroy에서 호출.
        /// </summary>
        void DisposeBaseline();

        /// <summary>
        /// Renderer의 sharedMaterials를 교체한다.
        /// slot mismatch 검증/차단 없이 그대로 설정한다.
        /// cloneOnApply가 true면 각 Material을 clone하여 적용한다.
        /// </summary>
        void SetSharedMaterials(Material[] materials);

        /// <summary>
        /// Renderer의 enabled 상태를 설정한다.
        /// </summary>
        void SetVisible(bool visible);
    }
}
