var WebSocketPlugin = {
    $webSocketState: {
        sockets: {},
        nextSocketId: 1,
        gameObjectName: null
    },

    WS_SetGameObject: function(goNamePtr) {
        webSocketState.gameObjectName = UTF8ToString(goNamePtr);
    },

    WS_Connect: function(urlPtr, subProtocolsJsonPtr) {
        var url = UTF8ToString(urlPtr);
        var subProtocolsJson = UTF8ToString(subProtocolsJsonPtr);
        var subProtocols = [];
        
        if (subProtocolsJson && subProtocolsJson.length > 0) {
            try {
                subProtocols = JSON.parse(subProtocolsJson);
            } catch (e) {
                // ignore parse error
            }
        }

        var socketId = webSocketState.nextSocketId++;
        var socket;

        try {
            if (subProtocols.length > 0) {
                socket = new WebSocket(url, subProtocols);
            } else {
                socket = new WebSocket(url);
            }
        } catch (e) {
            // Connection failed immediately
            setTimeout(function() {
                if (webSocketState.gameObjectName) {
                    SendMessage(webSocketState.gameObjectName, 'OnWsError', JSON.stringify({
                        socketId: socketId,
                        message: e.message || 'Connection failed'
                    }));
                }
            }, 0);
            return socketId;
        }

        socket.binaryType = 'arraybuffer';
        webSocketState.sockets[socketId] = socket;

        socket.onopen = function() {
            if (webSocketState.gameObjectName) {
                SendMessage(webSocketState.gameObjectName, 'OnWsOpen', socketId.toString());
            }
        };

        socket.onclose = function(event) {
            if (webSocketState.gameObjectName) {
                SendMessage(webSocketState.gameObjectName, 'OnWsClose', JSON.stringify({
                    socketId: socketId,
                    code: event.code,
                    reason: event.reason || ''
                }));
            }
            delete webSocketState.sockets[socketId];
        };

        socket.onerror = function(event) {
            if (webSocketState.gameObjectName) {
                SendMessage(webSocketState.gameObjectName, 'OnWsError', JSON.stringify({
                    socketId: socketId,
                    message: 'WebSocket error'
                }));
            }
        };

        socket.onmessage = function(event) {
            if (event.data instanceof ArrayBuffer) {
                var bytes = new Uint8Array(event.data);
                var len = bytes.length;
                
                // Allocate on WASM heap and copy
                var ptr = _malloc(len);
                HEAPU8.set(bytes, ptr);

                if (webSocketState.gameObjectName) {
                    SendMessage(webSocketState.gameObjectName, 'OnWsMessage', JSON.stringify({
                        socketId: socketId,
                        ptr: ptr,
                        len: len
                    }));
                }
                // Note: C# must call WS_FreeBuffer(ptr) after processing
            }
        };

        return socketId;
    },

    WS_GetState: function(socketId) {
        var socket = webSocketState.sockets[socketId];
        if (!socket) return 3; // CLOSED
        return socket.readyState;
    },

    WS_SendBinary: function(socketId, ptr, len) {
        var socket = webSocketState.sockets[socketId];
        if (!socket || socket.readyState !== 1) return 0; // Not OPEN

        // Read directly from WASM heap (zero-copy view)
        var view = HEAPU8.subarray(ptr, ptr + len);

        try {
            socket.send(view);
            return 1;
        } catch (e) {
            return 0;
        }
    },

    WS_Close: function(socketId, code, reasonPtr) {
        var socket = webSocketState.sockets[socketId];
        if (!socket) return;

        var reason = reasonPtr ? UTF8ToString(reasonPtr) : '';

        try {
            if (code > 0) {
                socket.close(code, reason);
            } else {
                socket.close();
            }
        } catch (e) {
            // ignore
        }
    },

    WS_FreeBuffer: function(ptr) {
        if (ptr) {
            _free(ptr);
        }
    }
};

autoAddDeps(WebSocketPlugin, '$webSocketState');
mergeInto(LibraryManager.library, WebSocketPlugin);
