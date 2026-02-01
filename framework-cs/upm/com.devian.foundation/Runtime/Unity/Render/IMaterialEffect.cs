namespace Devian
{
    public interface IMaterialEffect
    {
        int Priority { get; }
        void Apply(IMaterialEffectDriver driver);

        // pooling reset hook
        void Reset();
    }
}
