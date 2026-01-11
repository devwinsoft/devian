/**
 * Example Network Client
 * 
 * WebSocket client for testing Proxy/Stub + defaultCodec + frame format.
 * Uses NetworkClient + ClientRuntime for simplified message handling.
 */

import WebSocket from 'ws';
import { NetworkClient, defaultCodec } from '@devian/network';
import { createClientRuntime } from '@devian/network-game';

// Environment variables
const WS_URL = process.env.WS_URL ?? 'ws://localhost:8080';
const USER_ID = process.env.USER_ID ?? 'user1';
const VERSION = parseInt(process.env.VERSION ?? '1', 10);

async function main() {
    console.log(`[Client] Connecting to ${WS_URL}...`);

    // Create client runtime for 'Game' protocol group
    const { runtime, game2CStub, c2GameProxyFactory } = createClientRuntime(defaultCodec);

    // Register unknown opcode handler (optional - runtime has default warn)
    runtime.setUnknownInboundOpcode(async (e) => {
        console.warn(`[Client] Unknown opcode=${e.opcode} bytes=${e.payload.length} - ignoring`);
    });

    // Create WebSocket connection
    const ws = new WebSocket(WS_URL);

    // Create NetworkClient for message handling
    const client = new NetworkClient(runtime, {
        sessionId: 0,
        onError: (err) => console.error(`[Client] Network error:`, err),
    });

    // Register handlers for server messages (inbound: Game2C)
    game2CStub.onLoginResponse((sessionId, msg) => {
        console.log(`[Handler] LoginResponse:`, {
            success: msg.success,
            playerId: msg.playerId.toString(),
            errorCode: msg.errorCode,
            errorMessage: msg.errorMessage,
        });
    });

    game2CStub.onJoinRoomResponse((sessionId, msg) => {
        console.log(`[Handler] JoinRoomResponse:`, {
            success: msg.success,
            roomId: msg.roomId,
            playerIds: msg.playerIds.map(id => id.toString()),
        });
    });

    game2CStub.onChatNotify((sessionId, msg) => {
        console.log(`[Handler] ChatNotify:`, {
            channel: msg.channel,
            senderId: msg.senderId.toString(),
            senderName: msg.senderName,
            message: msg.message,
            timestamp: msg.timestamp.toString(),
        });
    });

    // Create outbound proxy (C2Game)
    const sendFn = (sessionId: number, frame: Uint8Array) => {
        if (ws.readyState === WebSocket.OPEN) {
            ws.send(frame);
        }
    };
    const c2gameProxy = c2GameProxyFactory(sendFn);

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

    // Delegate message handling to NetworkClient
    ws.on('message', (raw) => client.onWsMessage(raw));

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
