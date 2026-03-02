#nullable enable

namespace Devian
{
    /// <summary>
    /// Contract for outbound protocol objects that can be bound to a live session.
    /// Keeps session lifecycle ownership outside generated proxies.
    /// </summary>
    public interface INetSessionBindable
    {
        /// <summary>
        /// Bind a live session for outbound sends.
        /// </summary>
        void AttachSession(INetSession session);

        /// <summary>
        /// Remove the currently bound session.
        /// </summary>
        void DetachSession();
    }
}
