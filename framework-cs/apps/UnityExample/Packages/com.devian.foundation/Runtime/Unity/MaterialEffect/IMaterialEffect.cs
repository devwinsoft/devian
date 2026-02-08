namespace Devian
{
    public interface IMaterialEffect
    {
        int Priority { get; }
        void Apply(BaseMaterialEffectDriver driver);

        // pooling reset hook
        void Reset();
    }
}
