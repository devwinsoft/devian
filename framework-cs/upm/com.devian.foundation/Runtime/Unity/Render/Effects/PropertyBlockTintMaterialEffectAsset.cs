using UnityEngine;

namespace Devian
{
    /// <summary>
    /// MaterialPropertyBlock을 사용하여 색상 틴트를 적용하는 MaterialEffect.
    /// Material 인스턴스를 생성하지 않아 성능에 유리하다.
    /// </summary>
    [CreateAssetMenu(fileName = "PropertyBlockTintMaterialEffect", menuName = "Devian/Material Effects/Property Block Tint")]
    public sealed class PropertyBlockTintMaterialEffectAsset : MaterialEffectAsset
    {
        [Tooltip("Property name for color tint (e.g. _Color, _BaseColor).")]
        [SerializeField] private string _colorPropertyName = "_Color";

        [Tooltip("Tint color to apply.")]
        [SerializeField] private Color _tintColor = Color.white;

        public string ColorPropertyName => _colorPropertyName;
        public Color TintColor => _tintColor;

        protected override IMaterialEffect CreateInstanceInternal()
        {
            return new PropertyBlockTintMaterialEffect(Priority, _colorPropertyName, _tintColor);
        }

        private sealed class PropertyBlockTintMaterialEffect : IMaterialEffect
        {
            private readonly int _priority;
            private readonly string _colorPropertyName;
            private readonly int _colorPropertyId;
            private Color _tintColor;
            private MaterialPropertyBlock _propertyBlock;

            public int Priority => _priority;

            public PropertyBlockTintMaterialEffect(int priority, string colorPropertyName, Color tintColor)
            {
                _priority = priority;
                _colorPropertyName = colorPropertyName;
                _colorPropertyId = Shader.PropertyToID(colorPropertyName);
                _tintColor = tintColor;
                _propertyBlock = new MaterialPropertyBlock();
            }

            public void Apply(IMaterialEffectDriver driver)
            {
                if (driver == null)
                    return;

                _propertyBlock.Clear();
                _propertyBlock.SetColor(_colorPropertyId, _tintColor);

                for (int i = 0; i < driver.RendererCount; i++)
                {
                    driver.SetPropertyBlock(i, _propertyBlock);
                }
            }

            public void Reset()
            {
                _propertyBlock.Clear();
            }
        }
    }
}
