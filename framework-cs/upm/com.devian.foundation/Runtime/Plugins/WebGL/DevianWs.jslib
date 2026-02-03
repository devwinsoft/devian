/**
 * DevianWs.jslib - WebGL WebSocket Bridge (Polling-based)
 *
 * Provides polling-based WebSocket API for Unity WebGL builds.
 * No Unity callback dispatch - all events are queued and polled via WS_PollEvent.
 *
 * Memory rules:
 * - Queue stores raw data (ArrayBuffer/string), not pointers
 * - Pointers are allocated with _malloc at WS_PollEvent call time
 * - Binary data: C# calls WS_FreeBuffer after Marshal.Copy
 * - String data: C# calls WS_FreeString after PtrToStringUTF8
 */

var DevianWsLib = {
    // ========== Global State ==========
    $DevianWs: {
        sockets: {},
        nextId: 1,

        // Event type constants (must match C# EVT_* constants)
        EVT_OPEN: 1,
        EVT_CLOSE: 2,
        EVT_ERROR: 3,
        EVT_MESSAGE: 4,

        // Allocate UTF8 string and return pointer (caller must free via WS_FreeString)
        allocateString: function(str) {
            if (!str) return 0;
            var len = lengthBytesUTF8(str) + 1;
            var ptr = _malloc(len);
            if (ptr) {
                stringToUTF8(str, ptr, len);
            }
            return ptr;
        },

        // Allocate binary buffer and return pointer (caller must free via WS_FreeBuffer)
        allocateBuffer: function(arrayBuffer) {
            if (!arrayBuffer || arrayBuffer.byteLength === 0) return { ptr: 0, len: 0 };
            var len = arrayBuffer.byteLength;
            var ptr = _malloc(len);
            if (ptr) {
                var src = new Uint8Array(arrayBuffer);
                HEAPU8.set(src, ptr);
            }
            return { ptr: ptr, len: len };
        }
    },

    // ========== Exported Functions ==========

    /**
     * WS_Connect - Create and connect a WebSocket
     * @param {number} urlPtr - Pointer to UTF8 URL string
     * @param {number} subProtocolsJsonPtr - Pointer to UTF8 JSON array string (e.g., '["proto1"]' or '')
     * @returns {number} Socket ID (>= 0) on success, -1 on failure
     */
    WS_Connect: function(urlPtr, subProtocolsJsonPtr) {
        var url = UTF8ToString(urlPtr);
        var subProtocolsJson = UTF8ToString(subProtocolsJsonPtr);

        // Parse sub-protocols
        var subProtocols = undefined;
        if (subProtocolsJson && subProtocolsJson.length > 0) {
            try {
                var parsed = JSON.parse(subProtocolsJson);
                if (Array.isArray(parsed) && parsed.length > 0) {
                    subProtocols = parsed;
                }
            } catch (e) {
                // Invalid JSON, ignore sub-protocols
            }
        }

        var socketId = DevianWs.nextId++;
        var socket = {
            ws: null,
            queue: [],
            closed: false
        };

        try {
            socket.ws = subProtocols ? new WebSocket(url, subProtocols) : new WebSocket(url);
            socket.ws.binaryType = 'arraybuffer';
        } catch (e) {
            // Connection failed immediately
            socket.queue.push({
                t: DevianWs.EVT_ERROR,
                code: 0,
                msg: 'WebSocket creation failed: ' + e.message
            });
            socket.queue.push({
                t: DevianWs.EVT_CLOSE,
                code: 1006,
                msg: 'Connection failed'
            });
            socket.closed = true;
            DevianWs.sockets[socketId] = socket;
            return socketId;
        }

        // Event handlers - queue events for polling (no callbacks)
        socket.ws.onopen = function() {
            socket.queue.push({
                t: DevianWs.EVT_OPEN,
                code: 0
            });
        };

        socket.ws.onclose = function(evt) {
            socket.queue.push({
                t: DevianWs.EVT_CLOSE,
                code: evt.code || 1000,
                msg: evt.reason || ''
            });
            socket.closed = true;
        };

        socket.ws.onerror = function() {
            // WebSocket error events don't carry detailed info
            socket.queue.push({
                t: DevianWs.EVT_ERROR,
                code: 0,
                msg: 'WebSocket error (socketId=' + socketId + ')'
            });
        };

        socket.ws.onmessage = function(evt) {
            if (evt.data instanceof ArrayBuffer) {
                // Binary message - store ArrayBuffer directly
                socket.queue.push({
                    t: DevianWs.EVT_MESSAGE,
                    code: 0,
                    data: evt.data
                });
            } else if (typeof evt.data === 'string') {
                // Text message - encode to UTF8 ArrayBuffer
                var encoder = new TextEncoder();
                var encoded = encoder.encode(evt.data);
                socket.queue.push({
                    t: DevianWs.EVT_MESSAGE,
                    code: 0,
                    data: encoded.buffer
                });
            }
            // Blob type is not expected with binaryType='arraybuffer', but ignore if received
        };

        DevianWs.sockets[socketId] = socket;
        return socketId;
    },

    /**
     * WS_GetState - Get WebSocket ready state
     * @param {number} socketId - Socket ID
     * @returns {number} readyState (0=CONNECTING, 1=OPEN, 2=CLOSING, 3=CLOSED)
     */
    WS_GetState: function(socketId) {
        var socket = DevianWs.sockets[socketId];
        if (!socket || !socket.ws) return 3; // CLOSED
        return socket.ws.readyState;
    },

    /**
     * WS_SendBinary - Send binary data
     * @param {number} socketId - Socket ID
     * @param {number} ptr - Pointer to binary data
     * @param {number} len - Length of data
     * @returns {number} 1 on success, 0 on failure
     */
    WS_SendBinary: function(socketId, ptr, len) {
        var socket = DevianWs.sockets[socketId];
        if (!socket || !socket.ws) return 0;
        if (socket.ws.readyState !== 1) return 0; // Not OPEN

        try {
            // Copy from HEAPU8 to new buffer (safe, avoids detached buffer issues)
            var data = new Uint8Array(len);
            data.set(HEAPU8.subarray(ptr, ptr + len));
            socket.ws.send(data);
            return 1;
        } catch (e) {
            return 0;
        }
    },

    /**
     * WS_Close - Close WebSocket connection
     * @param {number} socketId - Socket ID
     * @param {number} code - Close code (e.g., 1000)
     * @param {number} reasonPtr - Pointer to UTF8 reason string
     */
    WS_Close: function(socketId, code, reasonPtr) {
        var socket = DevianWs.sockets[socketId];
        if (!socket || !socket.ws) return;

        var reason = UTF8ToString(reasonPtr);

        try {
            if (socket.ws.readyState === 0 || socket.ws.readyState === 1) {
                socket.ws.close(code, reason);
            }
        } catch (e) {
            // Ignore close errors
        }
    },

    /**
     * WS_PollEvent - Poll one event from the queue
     * @param {number} socketId - Socket ID
     * @param {number} eventTypePtr - Out: event type (1=OPEN, 2=CLOSE, 3=ERROR, 4=MESSAGE)
     * @param {number} codePtr - Out: close code (for CLOSE events)
     * @param {number} dataPtrPtr - Out: pointer to binary data (for MESSAGE events, caller must free)
     * @param {number} dataLenPtr - Out: length of binary data
     * @param {number} messagePtrPtr - Out: pointer to UTF8 string (for CLOSE/ERROR events, caller must free)
     * @returns {number} 1 if event was returned, 0 if queue is empty
     */
    WS_PollEvent: function(socketId, eventTypePtr, codePtr, dataPtrPtr, dataLenPtr, messagePtrPtr) {
        var socket = DevianWs.sockets[socketId];
        if (!socket) return 0;

        if (socket.queue.length === 0) return 0;

        var evt = socket.queue.shift();

        // Write event type
        HEAP32[eventTypePtr >> 2] = evt.t;

        // Write code (close code for CLOSE events, 0 for others)
        HEAP32[codePtr >> 2] = evt.code || 0;

        // Initialize out pointers to 0
        HEAP32[dataPtrPtr >> 2] = 0;
        HEAP32[dataLenPtr >> 2] = 0;
        HEAP32[messagePtrPtr >> 2] = 0;

        switch (evt.t) {
            case DevianWs.EVT_OPEN:
                // No additional data
                break;

            case DevianWs.EVT_CLOSE:
            case DevianWs.EVT_ERROR:
                // Allocate string for message (caller must free via WS_FreeString)
                if (evt.msg) {
                    var msgPtr = DevianWs.allocateString(evt.msg);
                    HEAP32[messagePtrPtr >> 2] = msgPtr;
                }
                break;

            case DevianWs.EVT_MESSAGE:
                // Allocate buffer for binary data (caller must free via WS_FreeBuffer)
                if (evt.data) {
                    var buf = DevianWs.allocateBuffer(evt.data);
                    HEAP32[dataPtrPtr >> 2] = buf.ptr;
                    HEAP32[dataLenPtr >> 2] = buf.len;
                }
                break;
        }

        return 1;
    },

    /**
     * WS_FreeBuffer - Free a buffer allocated by WS_PollEvent (binary data)
     * @param {number} ptr - Pointer to free
     */
    WS_FreeBuffer: function(ptr) {
        if (ptr !== 0) {
            _free(ptr);
        }
    },

    /**
     * WS_FreeString - Free a string allocated by WS_PollEvent (error/close messages)
     * @param {number} ptr - Pointer to free
     */
    WS_FreeString: function(ptr) {
        if (ptr !== 0) {
            _free(ptr);
        }
    }
};

// Register dependency on DevianWs global object
autoAddDeps(DevianWsLib, '$DevianWs');

// Merge into Emscripten runtime
mergeInto(LibraryManager.library, DevianWsLib);
