/**
 * Example Network Client
 * 
 * WebSocket client for testing Proxy/Stub + defaultCodec + frame format.
 * Connects to ExampleNetworkServer and performs round-trip communication.
 */

import WebSocket from 'ws';
import { defaultCodec } from '@devian/network-server';
import { C2Game, Game2C } from '@devian/protocol-client';
import { parseFrame } from './frame';

// Environment variables
const WS_URL = process.env.WS_URL ?? 'ws://localhost:8080';
const USER_ID = process.env.USER_ID ?? 'user1';
const VERSION = parseInt(process.env.VERSION ?? '1', 10);

async function main() {
    console.log(`[Client] Connecting to ${WS_URL}...`);

    const ws = new WebSocket(WS_URL);

    // Create Game2C.Stub for receiving server messages
    const game2cStub = new Game2C.Stub(defaultCodec);

    // Register handlers for server messages
    game2cStub.onLoginResponse((sessionId, msg) => {
        console.log(`[Handler] LoginResponse:`, {
            success: msg.success,
            playerId: msg.playerId.toString(),
            errorCode: msg.errorCode,
            errorMessage: msg.errorMessage,
        });
    });

    game2cStub.onJoinRoomResponse((sessionId, msg) => {
        console.log(`[Handler] JoinRoomResponse:`, {
            success: msg.success,
            roomId: msg.roomId,
            playerIds: msg.playerIds.map(id => id.toString()),
        });
    });

    game2cStub.onChatNotify((sessionId, msg) => {
        console.log(`[Handler] ChatNotify:`, {
            channel: msg.channel,
            senderId: msg.senderId.toString(),
            senderName: msg.senderName,
            message: msg.message,
            timestamp: msg.timestamp.toString(),
        });
    });

    // Create send function for C2Game.Proxy
    const sendFn = (sessionId: number, frame: Uint8Array) => {
        if (ws.readyState === WebSocket.OPEN) {
            ws.send(frame);
        }
    };

    // Create C2Game.Proxy for sending client messages
    const c2gameProxy = new C2Game.Proxy(sendFn, defaultCodec);

    // WebSocket event handlers
    ws.on('open', () => {
        console.log(`[Client] Connected to server`);

        // Send LoginRequest
        console.log(`[Client] Sending LoginRequest...`);
        c2gameProxy.sendLoginRequest(0, {
            userId: USER_ID,
            token: 'test-token',
            version: VERSION,
        });

        // Send ChatMessage after 1 second
        setTimeout(() => {
            console.log(`[Client] Sending ChatMessage...`);
            c2gameProxy.sendChatMessage(0, {
                channel: 0,
                message: 'Hello from client!',
                targetUserIds: undefined,
            });
        }, 1000);
    });

    ws.on('message', (data: Buffer | ArrayBuffer | Buffer[]) => {
        try {
            // Convert to Uint8Array
            let bytes: Uint8Array;
            if (data instanceof Buffer) {
                bytes = new Uint8Array(data);
            } else if (data instanceof ArrayBuffer) {
                bytes = new Uint8Array(data);
            } else if (Array.isArray(data)) {
                bytes = new Uint8Array(Buffer.concat(data));
            } else {
                console.warn(`[Client] Unknown message type`);
                return;
            }

            // Parse frame
            const frame = parseFrame(bytes);
            if (!frame) {
                console.warn(`[Client] Invalid frame received`);
                return;
            }

            const { opcode, payload } = frame;

            // Check if opcode is known
            const opcodeName = Game2C.getOpcodeName(opcode);
            if (!opcodeName) {
                // Unknown opcode - log and ignore (NEVER disconnect)
                console.warn(`[Client] Unknown opcode ${opcode} (${payload.length} bytes) - ignoring`);
                return;
            }

            // Dispatch to stub (sessionId=0 for client)
            game2cStub.dispatch(0, opcode, payload);
        } catch (error) {
            console.error(`[Client] Error processing message:`, error);
        }
    });

    ws.on('error', (error) => {
        console.error(`[Client] WebSocket error:`, error.message);
    });

    ws.on('close', (code, reason) => {
        console.log(`[Client] Connection closed: ${code} ${reason.toString()}`);
        process.exit(0);
    });

    // Graceful shutdown
    process.on('SIGINT', () => {
        console.log('\n[Client] Shutting down...');
        ws.close();
    });
}

main().catch(console.error);
