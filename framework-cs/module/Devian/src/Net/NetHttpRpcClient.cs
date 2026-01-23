#nullable enable
using System;
using System.Net.Http;
using System.Text;

namespace Devian
{
    /// <summary>
    /// HTTP RPC client with binary→base64 single-param POST protocol.
    /// All public APIs are synchronous.
    /// 
    /// Wire Protocol:
    /// - Request: POST with application/x-www-form-urlencoded body
    ///   Body format: {paramName}={urlEncoded(base64(requestBinary))}
    /// - Response: Plain text base64, decoded to binary
    /// </summary>
    public sealed class NetHttpRpcClient : IDisposable
    {
        private readonly HttpClient _http;
        private readonly Uri _endpoint;
        private readonly string _paramName;
        private readonly bool _ownsHttp;

        /// <summary>
        /// Creates a new NetHttpRpcClient.
        /// </summary>
        /// <param name="endpoint">RPC endpoint URI.</param>
        /// <param name="paramName">Form parameter name (default: "p").</param>
        /// <param name="http">Optional HttpClient (if null, creates internal one).</param>
        public NetHttpRpcClient(Uri endpoint, string paramName = "p", HttpClient? http = null)
        {
            _endpoint = endpoint ?? throw new ArgumentNullException(nameof(endpoint));
            _paramName = string.IsNullOrWhiteSpace(paramName) ? "p" : paramName;

            if (http == null)
            {
                _http = new HttpClient();
                _ownsHttp = true;
            }
            else
            {
                _http = http;
                _ownsHttp = false;
            }
        }

        /// <summary>
        /// Synchronously call the RPC endpoint.
        /// Throws on HTTP error or decode failure.
        /// </summary>
        /// <param name="requestBinary">Request payload.</param>
        /// <returns>Response payload (decoded from base64).</returns>
        public byte[] Call(ReadOnlySpan<byte> requestBinary)
        {
            // binary -> base64
            var b64 = Convert.ToBase64String(requestBinary);

            // form-urlencoded body: {paramName}={urlEncoded(base64)}
            var body = $"{Uri.EscapeDataString(_paramName)}={Uri.EscapeDataString(b64)}";
            using var content = new StringContent(body, Encoding.UTF8, "application/x-www-form-urlencoded");

            // sync POST (internal async)
            using var resp = _http.PostAsync(_endpoint, content).GetAwaiter().GetResult();
            resp.EnsureSuccessStatusCode();

            // response: base64 text -> binary
            var text = resp.Content.ReadAsStringAsync().GetAwaiter().GetResult();
            return Convert.FromBase64String(text.Trim());
        }

        /// <summary>
        /// Try to call the RPC endpoint without throwing.
        /// </summary>
        /// <param name="requestBinary">Request payload.</param>
        /// <param name="responseBinary">Response payload if successful.</param>
        /// <returns>True if successful, false otherwise.</returns>
        public bool TryCall(ReadOnlySpan<byte> requestBinary, out byte[] responseBinary)
        {
            try
            {
                responseBinary = Call(requestBinary);
                return true;
            }
            catch
            {
                responseBinary = Array.Empty<byte>();
                return false;
            }
        }

        /// <summary>
        /// Dispose the client (releases HttpClient if owned).
        /// </summary>
        public void Dispose()
        {
            if (_ownsHttp)
                _http.Dispose();
        }
    }
}
