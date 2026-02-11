#nullable enable

namespace Devian
{
    /// <summary>
    /// Interface for network objects that require periodic tick processing.
    /// Used by tick loops (manual Update, custom runner, etc.) for unified tick management.
    /// </summary>
    public interface INetTickable
    {
        /// <summary>
        /// Process pending events and dispatch queued callbacks.
        /// Should be called once per frame from the main thread.
        /// </summary>
        void Tick();
    }
}
