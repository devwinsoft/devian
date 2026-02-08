#nullable enable

namespace Devian
{
    /// <summary>
    /// WebSocket-based connector implementation.
    /// Creates sessions using NetWsTransport and BaseNetClient.
    /// </summary>
    public sealed class NetWsConnector : INetConnector
    {
        /// <inheritdoc />
        public INetSession CreateSession(INetRuntime runtime, string url)
        {
            var core = new NetClient(runtime);
            var transport = new NetWsTransport(core);
            return new BaseNetClient(transport, url);
        }
    }
}
