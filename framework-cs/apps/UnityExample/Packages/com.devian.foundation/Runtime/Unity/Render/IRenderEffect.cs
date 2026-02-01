namespace Devian
{
    public interface IRenderEffect
    {
        int Priority { get; }
        void Apply(IRenderDriver driver);

        // pooling reset hook
        void Reset();
    }
}
