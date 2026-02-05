#nullable enable

namespace Devian
{
    /// <summary>
    /// Represents the connection state of a network client.
    /// </summary>
    public enum NetClientState
    {
        /// <summary>Not connected.</summary>
        Disconnected,

        /// <summary>Connection in progress.</summary>
        Connecting,

        /// <summary>Connected and ready.</summary>
        Connected,

        /// <summary>Graceful close in progress.</summary>
        Closing,

        /// <summary>Connection failed or error occurred.</summary>
        Faulted
    }
}
