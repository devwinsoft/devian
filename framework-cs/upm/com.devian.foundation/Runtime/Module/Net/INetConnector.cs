#nullable enable

namespace Devian
{
    /// <summary>
    /// Factory interface for creating network sessions.
    /// Allows protocol code to create sessions without depending on concrete implementations.
    /// </summary>
    public interface INetConnector
    {
        /// <summary>
        /// Create a new network session.
        /// </summary>
        /// <param name="runtime">Protocol runtime for message dispatch.</param>
        /// <param name="url">Server URL to connect to.</param>
        /// <returns>A new session instance (not yet connected).</returns>
        INetSession CreateSession(INetRuntime runtime, string url);
    }
}
