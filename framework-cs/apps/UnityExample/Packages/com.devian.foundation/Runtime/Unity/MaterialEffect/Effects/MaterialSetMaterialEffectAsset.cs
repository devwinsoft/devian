using UnityEngine;

namespace Devian
{
    /// <summary>
    /// Material[] 배열을 그대로 Renderer에 적용하는 MaterialEffect (v2 표준).
    /// slot mismatch 검증/차단 없이 그대로 SetSharedMaterials()를 호출한다.
    /// </summary>
    [CreateAssetMenu(fileName = "MaterialSetMaterialEffect", menuName = "Devian/Material Effects/Material Set")]
    public sealed class MaterialSetMaterialEffectAsset : MaterialEffectAsset
    {
        [Tooltip("Materials to apply. Length does not need to match renderer's slot count.")]
        [SerializeField] private Material[] _materials;

        public Material[] Materials => _materials;

#if UNITY_EDITOR
        /// <summary>
        /// Editor-only: Set materials array for snapshot save.
        /// </summary>
        public void SetMaterialsForEditor(Material[] materials)
        {
            _materials = materials;
            UnityEditor.EditorUtility.SetDirty(this);
        }
#endif

        protected override IMaterialEffect CreateInstanceInternal()
        {
            return new MaterialSetMaterialEffect(Priority, _materials);
        }

        private sealed class MaterialSetMaterialEffect : IMaterialEffect
        {
            private readonly int _priority;
            private readonly Material[] _materials;

            public int Priority => _priority;

            public MaterialSetMaterialEffect(int priority, Material[] materials)
            {
                _priority = priority;
                _materials = materials;
            }

            public void Apply(IMaterialEffectDriver driver)
            {
                if (driver == null || _materials == null)
                    return;

                // slot mismatch 검증/차단 없이 그대로 적용
                driver.SetSharedMaterials(_materials);
            }

            public void Reset()
            {
                // _materials는 Asset에서 온 읽기 전용 참조이므로 정리할 것 없음
            }
        }
    }
}
