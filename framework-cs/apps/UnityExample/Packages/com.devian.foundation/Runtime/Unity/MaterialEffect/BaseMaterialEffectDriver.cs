using UnityEngine;

namespace Devian
{
    /// <summary>
    /// 단일 Renderer를 위한 Material 교체 Driver 베이스 (v2).
    /// PropertyBlock/Common 관련 API는 제거됨.
    /// </summary>
    public abstract class BaseMaterialEffectDriver : MonoBehaviour
    {
        public abstract bool IsValid { get; }

        public abstract void CaptureBaseline();
        public abstract void RestoreBaseline();
        public abstract void DisposeBaseline();

        public abstract void SetSharedMaterials(Material[] materials);
        public abstract void SetVisible(bool visible);
    }
}
