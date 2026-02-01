using UnityEngine;

namespace Devian
{
    /// <summary>
    /// MaterialPropertyBlock을 사용하여 색상 틴트를 적용하는 RenderEffect.
    /// Material 인스턴스를 생성하지 않아 성능에 유리하다.
    /// </summary>
    [CreateAssetMenu(fileName = "PropertyBlockTintRenderEffect", menuName = "Devian/Render Effects/Property Block Tint")]
    public sealed class PropertyBlockTintRenderEffectAsset : RenderEffectAsset
    {
        [Tooltip("Property name for color tint (e.g. _Color, _BaseColor).")]
        [SerializeField] private string _colorPropertyName = "_Color";

        [Tooltip("Tint color to apply.")]
        [SerializeField] private Color _tintColor = Color.white;

        public string ColorPropertyName => _colorPropertyName;
        public Color TintColor => _tintColor;

        protected override IRenderEffect CreateInstanceInternal()
        {
            return new PropertyBlockTintRenderEffect(Priority, _colorPropertyName, _tintColor);
        }

        private sealed class PropertyBlockTintRenderEffect : IRenderEffect
        {
            private readonly int _priority;
            private readonly string _colorPropertyName;
            private readonly int _colorPropertyId;
            private Color _tintColor;
            private MaterialPropertyBlock _propertyBlock;

            public int Priority => _priority;

            public PropertyBlockTintRenderEffect(int priority, string colorPropertyName, Color tintColor)
            {
                _priority = priority;
                _colorPropertyName = colorPropertyName;
                _colorPropertyId = Shader.PropertyToID(colorPropertyName);
                _tintColor = tintColor;
                _propertyBlock = new MaterialPropertyBlock();
            }

            public void Apply(IRenderDriver driver)
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
