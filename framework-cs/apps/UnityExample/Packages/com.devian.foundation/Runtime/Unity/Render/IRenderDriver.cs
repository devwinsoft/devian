using UnityEngine;

namespace Devian
{
    public interface IRenderDriver
    {
        bool IsValid { get; }

        // baseline capture/restore
        void CaptureBaseline();
        void RestoreBaseline();

        // renderers
        int RendererCount { get; }

        // material override
        void SetSharedMaterial(int rendererIndex, Material material);
        void SetSharedMaterials(int rendererIndex, Material[] materials);

        // property block
        void ClearPropertyBlock(int rendererIndex);
        void SetPropertyBlock(int rendererIndex, MaterialPropertyBlock block);

        // optional
        void SetVisible(bool visible);
    }
}
