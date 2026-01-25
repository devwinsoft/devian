/**
 * Game Server
 * 
 * This app demonstrates how to assemble devian-network
 * with the Game protocol group's server runtime.
 * 
 * This file only:
 * - Assembles transport + network runtime
 * - Registers message handlers (Ping → Pong, Echo → EchoReply)
 * - Handles unknown opcodes via hook (no disconnect)
 */

import {
    WsTransport,
    NetworkServer,
    defaultCodec as jsonCodec,
    type UnknownOpcodeEvent,
} from '@devian/core';

import {
    createServerRuntime,
    Game2C,
} from '@devian/network-game/server-runtime';

const PORT = 8080;

// Toggle codec: false = Protobuf (default), true = Json
const USE_JSON = false;

async function main() {
    // 1. Create network runtime for 'Game' group
    const runtime = USE_JSON ? createServerRuntime(jsonCodec) : createServerRuntime();
    console.log(`[GameServer] Using ${USE_JSON ? 'Json' : 'Protobuf'} codec`);

    // 2. Get stub for handler registration
    const stub = runtime.getStub();

    // 3. Create network server (will be wired after transport creation)
    let server: NetworkServer;

    // 4. Create single transport with events
    const transport = new WsTransport(
        { port: PORT },
        {
            onConnect: (sessionId) => {
                console.log(`[GameServer] Session ${sessionId} connected`);
            },
            onDisconnect: (sessionId) => {
                console.log(`[GameServer] Session ${sessionId} disconnected`);
            },
            onBinaryMessage: async (sessionId, data) => {
                // Delegate to network server
                await server.onBinaryMessage(sessionId, data);
            },
            onError: (sessionId, error) => {
                console.error(`[GameServer] Session ${sessionId} error:`, error.message);
            },
        }
    );

    // 5. Create network server with the same transport
    server = new NetworkServer(transport, runtime, {
        onUnknownInboundOpcode: async (event: UnknownOpcodeEvent) => {
            // Log only - NEVER disconnect (async hook supported)
            console.warn(
                `[GameServer] Unknown opcode ${event.opcode} from session ${event.sessionId}`,
                `(${event.payload.length} bytes) - ignoring`
            );
        },
    });

    // 6. Get typed outbound proxy (uses the same transport)
    const game2cProxy = server.createOutboundProxy<Game2C.Proxy>();

    // 7. Register message handlers on stub
    stub.onPing(async (sessionId, msg) => {
        console.log(`[Handler] Ping from session ${sessionId}:`, {
            timestamp: msg.timestamp.toString(),
            payload: msg.payload,
        });

        // Send Pong response
        await game2cProxy.sendPong(sessionId, {
            timestamp: msg.timestamp,
            serverTime: BigInt(Date.now()),
        });
    });

    stub.onEcho(async (sessionId, msg) => {
        console.log(`[Handler] Echo from session ${sessionId}:`, {
            message: msg.message,
        });

        // Send EchoReply response
        await game2cProxy.sendEchoReply(sessionId, {
            message: msg.message,
            echoedAt: BigInt(Date.now()),
        });
    });

    // 8. Start transport (single call)
    await transport.start();
    console.log(`[GameServer] Running on port ${PORT}`);

    // Graceful shutdown
    process.on('SIGINT', async () => {
        console.log('\n[GameServer] Shutting down...');
        await transport.stop();
        process.exit(0);
    });
}

main().catch(console.error);
