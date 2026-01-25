/**
 * Game Client
 * 
 * WebSocket client for testing Proxy/Stub + codec + frame format.
 * Uses NetworkClient + ClientRuntime for simplified message handling.
 * 
 * Communicates with GameServer using Game protocol:
 * - Outbound: C2Game (Ping, Echo)
 * - Inbound: Game2C (Pong, EchoReply)
 */

import WebSocket from 'ws';
import { NetworkClient, defaultCodec as jsonCodec } from '@devian/core';
import { createClientRuntime } from '@devian/network-game/client-runtime';

// Environment variables
const WS_URL = process.env.WS_URL ?? 'ws://localhost:8080';

// Toggle codec: false = Protobuf (default), true = Json
const USE_JSON = false;

async function main() {
    console.log(`[GameClient] Connecting to ${WS_URL}...`);

    // Create client runtime for 'Game' protocol group
    const { runtime, game2CStub, c2GameProxyFactory } = 
        USE_JSON ? createClientRuntime(jsonCodec) : createClientRuntime();
    console.log(`[GameClient] Using ${USE_JSON ? 'Json' : 'Protobuf'} codec`);

    // Register unknown opcode handler (optional - runtime has default warn)
    runtime.setUnknownInboundOpcode(async (e) => {
        console.warn(`[GameClient] Unknown opcode=${e.opcode} bytes=${e.payload.length} - ignoring`);
    });

    // Create WebSocket connection
    const ws = new WebSocket(WS_URL);

    // Create NetworkClient for message handling
    const client = new NetworkClient(runtime, {
        sessionId: 0,
        onError: (err) => console.error(`[GameClient] Network error:`, err),
    });

    // Register handlers for server messages (inbound: Game2C)
    game2CStub.onPong((sessionId, msg) => {
        console.log(`[Handler] Pong:`, {
            timestamp: msg.timestamp.toString(),
            serverTime: msg.serverTime.toString(),
        });
    });

    game2CStub.onEchoReply((sessionId, msg) => {
        console.log(`[Handler] EchoReply:`, {
            message: msg.message,
            echoedAt: msg.echoedAt.toString(),
        });
    });

    // Create outbound proxy (C2Game)
    const sendFn = (sessionId: number, frame: Uint8Array) => {
        if (ws.readyState === WebSocket.OPEN) {
            ws.send(frame);
        }
    };
    const c2GameProxy = c2GameProxyFactory(sendFn);

    // WebSocket event handlers
    ws.on('open', () => {
        console.log(`[GameClient] Connected to server`);

        // Send Ping
        console.log(`[GameClient] Sending Ping...`);
        c2GameProxy.sendPing(0, {
            timestamp: BigInt(Date.now()),
            payload: 'hello from client',
        });

        // Send Echo after 500ms
        setTimeout(() => {
            console.log(`[GameClient] Sending Echo...`);
            c2GameProxy.sendEcho(0, {
                message: 'echo from client',
            });
        }, 500);
    });

    // Delegate message handling to NetworkClient
    ws.on('message', (raw) => client.onWsMessage(raw));

    ws.on('error', (error) => {
        console.error(`[GameClient] WebSocket error:`, error.message);
    });

    ws.on('close', (code, reason) => {
        console.log(`[GameClient] Connection closed: ${code} ${reason.toString()}`);
        process.exit(0);
    });

    // Graceful shutdown
    process.on('SIGINT', () => {
        console.log('\n[GameClient] Shutting down...');
        ws.close();
    });
}

main().catch(console.error);
