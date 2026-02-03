using UnityEngine;

namespace Devian
{
    /// <summary>
    /// 아무 효과도 적용하지 않는 MaterialEffect.
    /// default effect로 사용하기에 적합하다.
    /// </summary>
    [CreateAssetMenu(fileName = "NoOpMaterialEffect", menuName = "Devian/Material Effects/NoOp")]
    public sealed class NoOpMaterialEffectAsset : MaterialEffectAsset
    {
        protected override IMaterialEffect CreateInstanceInternal()
        {
            return new NoOpMaterialEffect(Priority);
        }

        private sealed class NoOpMaterialEffect : IMaterialEffect
        {
            private readonly int _priority;

            public int Priority => _priority;

            public NoOpMaterialEffect(int priority)
            {
                _priority = priority;
            }

            public void Apply(IMaterialEffectDriver driver)
            {
                // 아무 것도 하지 않음 - baseline 상태 유지
            }

            public void Reset()
            {
                // 상태 없음
            }
        }
    }
}
