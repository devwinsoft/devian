using UnityEngine;

namespace Devian
{
    /// <summary>
    /// 모든 Renderer의 Material을 지정된 Material로 교체하는 MaterialEffect.
    /// </summary>
    [CreateAssetMenu(fileName = "MaterialSwapMaterialEffect", menuName = "Devian/Material Effects/Material Swap")]
    public sealed class MaterialSwapMaterialEffectAsset : MaterialEffectAsset
    {
        [Tooltip("Material to swap to.")]
        [SerializeField] private Material _material;

        public Material Material => _material;

        protected override IMaterialEffect CreateInstanceInternal()
        {
            return new MaterialSwapMaterialEffect(Priority, _material);
        }

        private sealed class MaterialSwapMaterialEffect : IMaterialEffect
        {
            private readonly int _priority;
            private Material _material;

            public int Priority => _priority;

            public MaterialSwapMaterialEffect(int priority, Material material)
            {
                _priority = priority;
                _material = material;
            }

            public void Apply(IMaterialEffectDriver driver)
            {
                if (_material == null || driver == null)
                    return;

                for (int i = 0; i < driver.RendererCount; i++)
                {
                    driver.SetSharedMaterial(i, _material);
                }
            }

            public void Reset()
            {
                // Material 참조는 Asset에서 온 것이므로 유지
            }
        }
    }
}
