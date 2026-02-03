#nullable enable
using System;
using UnityEngine;

namespace Devian
{
    /// <summary>
    /// Sample MonoBehaviour demonstrating GameNetworkClient usage with OnGUI buttons.
    /// Automatically creates a NetTickRunner if one doesn't exist in the scene.
    /// </summary>
    public class GameNetworkClientSample : MonoBehaviour
    {
        [Header("Connection")]
        [SerializeField]
        [Tooltip("WebSocket URL to connect to (e.g., ws://localhost:8080)")]
        private string _url = "ws://localhost:8080";

        [Header("Echo Settings")]
        [SerializeField]
        [Tooltip("Text to send with Echo message")]
        private string _echoText = "Hello, World!";

        // --- Runtime state ---
        private GameNetworkClient? _client;
        private NetTickRunner? _runner;

        // --- Status display ---
        private bool _isConnected;
        private string _lastError = "";
        private string _lastPong = "";
        private string _lastEchoReply = "";

        // --- Lifecycle ---

        private void Start()
        {
            // Find or create NetTickRunner
            _runner = FindAnyObjectByType<NetTickRunner>();
            if (_runner == null)
            {
                var go = new GameObject("NetTickRunner (Auto-created)");
                _runner = go.AddComponent<NetTickRunner>();
                Debug.Log("[GameNetworkClientSample] Created NetTickRunner automatically.");
            }

            // Create client
            _client = new GameNetworkClient();

            // Wire events
            _client.OnOpen += HandleOpen;
            _client.OnClose += HandleClose;
            _client.OnError += HandleError;
            _client.OnPong += HandlePong;
            _client.OnEchoReply += HandleEchoReply;

            // Register with runner for automatic Tick()
            _runner.Register(_client);
        }

        private void OnDestroy()
        {
            if (_client != null)
            {
                if (_runner != null)
                {
                    _runner.Unregister(_client);
                }
                _client.Dispose();
                _client = null;
            }
        }

        // --- Event handlers ---

        private void HandleOpen()
        {
            _isConnected = true;
            _lastError = "";
            Debug.Log("[GameNetworkClientSample] Connected!");
        }

        private void HandleClose(ushort code, string reason)
        {
            _isConnected = false;
            Debug.Log($"[GameNetworkClientSample] Closed: code={code}, reason={reason}");
        }

        private void HandleError(Exception ex)
        {
            _lastError = ex.Message;
            Debug.LogError($"[GameNetworkClientSample] Error: {ex.Message}");
        }

        private void HandlePong(Protocol.Game.Game2C.Pong pong)
        {
            var latency = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - pong.Timestamp;
            _lastPong = $"Latency: {latency}ms, ServerTime: {pong.ServerTime}";
            Debug.Log($"[GameNetworkClientSample] Pong received - {_lastPong}");
        }

        private void HandleEchoReply(Protocol.Game.Game2C.EchoReply reply)
        {
            _lastEchoReply = $"\"{reply.Message}\" (echoed at {reply.EchoedAt})";
            Debug.Log($"[GameNetworkClientSample] EchoReply: {_lastEchoReply}");
        }

        // --- OnGUI for simple UI ---

        private void OnGUI()
        {
            GUILayout.BeginArea(new Rect(10, 10, 350, 400));
            GUILayout.BeginVertical("box");

            GUILayout.Label("Game Network Client Sample", GUI.skin.box);
            GUILayout.Space(10);

            // Connection section
            GUILayout.Label("Connection", EditorStyleBold());
            _url = GUILayout.TextField(_url);

            GUILayout.BeginHorizontal();
            GUI.enabled = !_isConnected;
            if (GUILayout.Button("Connect"))
            {
                Connect();
            }
            GUI.enabled = _isConnected;
            if (GUILayout.Button("Disconnect"))
            {
                Disconnect();
            }
            GUI.enabled = true;
            GUILayout.EndHorizontal();

            GUILayout.Space(10);

            // Send section
            GUILayout.Label("Send Messages", EditorStyleBold());

            GUI.enabled = _isConnected;

            if (GUILayout.Button("Send Ping"))
            {
                SendPing();
            }

            GUILayout.BeginHorizontal();
            _echoText = GUILayout.TextField(_echoText, GUILayout.Width(200));
            if (GUILayout.Button("Send Echo"))
            {
                SendEcho();
            }
            GUILayout.EndHorizontal();

            GUI.enabled = true;

            GUILayout.Space(10);

            // Status section
            GUILayout.Label("Status", EditorStyleBold());
            GUILayout.Label($"Connected: {(_isConnected ? "Yes" : "No")}");

            if (!string.IsNullOrEmpty(_lastError))
            {
                GUILayout.Label($"Last Error: {_lastError}", ErrorStyle());
            }

            if (!string.IsNullOrEmpty(_lastPong))
            {
                GUILayout.Label($"Last Pong: {_lastPong}");
            }

            if (!string.IsNullOrEmpty(_lastEchoReply))
            {
                GUILayout.Label($"Last Echo: {_lastEchoReply}");
            }

            GUILayout.EndVertical();
            GUILayout.EndArea();
        }

        // --- Public API for Inspector/external use ---

        public void Connect()
        {
            if (_client == null || string.IsNullOrEmpty(_url)) return;

            _lastError = "";
            _lastPong = "";
            _lastEchoReply = "";

            try
            {
                _client.Connect(_url);
            }
            catch (Exception ex)
            {
                _lastError = ex.Message;
                Debug.LogError($"[GameNetworkClientSample] Connect failed: {ex.Message}");
            }
        }

        public void Disconnect()
        {
            _client?.Close();
        }

        public void SendPing()
        {
            _client?.SendPing();
        }

        public void SendEcho()
        {
            if (_client != null && !string.IsNullOrEmpty(_echoText))
            {
                _client.SendEcho(_echoText);
            }
        }

        // --- GUI Styles ---

        private static GUIStyle? _boldStyle;
        private static GUIStyle? _errorStyle;

        private static GUIStyle EditorStyleBold()
        {
            if (_boldStyle == null)
            {
                _boldStyle = new GUIStyle(GUI.skin.label)
                {
                    fontStyle = FontStyle.Bold
                };
            }
            return _boldStyle;
        }

        private static GUIStyle ErrorStyle()
        {
            if (_errorStyle == null)
            {
                _errorStyle = new GUIStyle(GUI.skin.label)
                {
                    normal = { textColor = Color.red }
                };
            }
            return _errorStyle;
        }
    }
}
