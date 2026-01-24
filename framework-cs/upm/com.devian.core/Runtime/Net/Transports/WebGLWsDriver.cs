#nullable enable
#if UNITY_WEBGL && !UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;

namespace Devian
{
    /// <summary>
    /// Internal singleton MonoBehaviour that receives JS WebSocket callbacks.
    /// Routes events to the appropriate NetWsClient instance.
    /// </summary>
    internal sealed class WebGLWsDriver : MonoBehaviour
    {
        private static WebGLWsDriver? _instance;
        private static readonly object _lock = new();

        private readonly Dictionary<int, NetWsClient> _clients = new();

        // JS interop declarations
        [DllImport("__Internal")]
        private static extern void WS_SetGameObject(string gameObjectName);

        /// <summary>
        /// Get or create the singleton driver instance.
        /// </summary>
        public static WebGLWsDriver Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_lock)
                    {
                        if (_instance == null)
                        {
                            var go = new GameObject("[WebGLWsDriver]");
                            DontDestroyOnLoad(go);
                            _instance = go.AddComponent<WebGLWsDriver>();
                            WS_SetGameObject(go.name);
                        }
                    }
                }
                return _instance;
            }
        }

        /// <summary>
        /// Register a client for the given socket ID.
        /// </summary>
        public void Register(int socketId, NetWsClient client)
        {
            lock (_lock)
            {
                _clients[socketId] = client;
            }
        }

        /// <summary>
        /// Unregister a client by socket ID.
        /// </summary>
        public void Unregister(int socketId)
        {
            lock (_lock)
            {
                _clients.Remove(socketId);
            }
        }

        private NetWsClient? GetClient(int socketId)
        {
            lock (_lock)
            {
                _clients.TryGetValue(socketId, out var client);
                return client;
            }
        }

        // ========== JS Callback Methods (called via SendMessage) ==========

        /// <summary>
        /// Called from JS when WebSocket opens.
        /// </summary>
        public void OnWsOpen(string socketIdStr)
        {
            if (int.TryParse(socketIdStr, out var socketId))
            {
                var client = GetClient(socketId);
                client?.HandleJsOpen();
            }
        }

        /// <summary>
        /// Called from JS when WebSocket closes.
        /// JSON: { socketId, code, reason }
        /// </summary>
        public void OnWsClose(string json)
        {
            try
            {
                var data = JsonUtility.FromJson<WsCloseData>(json);
                var client = GetClient(data.socketId);
                client?.HandleJsClose((ushort)data.code, data.reason ?? "");
            }
            catch
            {
                // ignore parse errors
            }
        }

        /// <summary>
        /// Called from JS when a binary message is received.
        /// JSON: { socketId, ptr, len }
        /// </summary>
        public void OnWsMessage(string json)
        {
            try
            {
                var data = JsonUtility.FromJson<WsMessagePtrData>(json);
                var client = GetClient(data.socketId);
                client?.HandleJsMessagePtr(data.ptr, data.len);
            }
            catch
            {
                // ignore parse errors
            }
        }

        /// <summary>
        /// Called from JS when a WebSocket error occurs.
        /// JSON: { socketId, message }
        /// </summary>
        public void OnWsError(string json)
        {
            try
            {
                var data = JsonUtility.FromJson<WsErrorData>(json);
                var client = GetClient(data.socketId);
                client?.HandleJsError(data.message ?? "Unknown error");
            }
            catch
            {
                // ignore parse errors
            }
        }

        // ========== JSON Data Classes ==========

        [Serializable]
        private class WsCloseData
        {
            public int socketId;
            public int code;
            public string? reason;
        }

        [Serializable]
        private class WsMessagePtrData
        {
            public int socketId;
            public int ptr;
            public int len;
        }

        [Serializable]
        private class WsErrorData
        {
            public int socketId;
            public string? message;
        }
    }
}
#endif
