/**
 * Example Network Server
 * 
 * This app demonstrates how to assemble devian-network
 * with a protocol group's server runtime.
 * 
 * This file only:
 * - Assembles transport + network runtime
 * - Registers message handlers
 * - Handles unknown opcodes via hook (no disconnect)
 */

import {
    WsTransport,
    NetworkServer,
    defaultCodec,
    type UnknownOpcodeEvent,
} from '@devian/network';

import {
    createServerRuntime,
    Game2C,
} from '@devian/network-game';

const PORT = 8080;

async function main() {
    // 1. Create network runtime for 'Game' group
    const runtime = createServerRuntime(defaultCodec);

    // 2. Get stub for handler registration
    const stub = runtime.getStub();

    // 3. Create network server (will be wired after transport creation)
    let server: NetworkServer;

    // 4. Create single transport with events
    const transport = new WsTransport(
        { port: PORT },
        {
            onConnect: (sessionId) => {
                console.log(`[App] Session ${sessionId} connected`);
            },
            onDisconnect: (sessionId) => {
                console.log(`[App] Session ${sessionId} disconnected`);
            },
            onBinaryMessage: async (sessionId, data) => {
                // Delegate to network server
                await server.onBinaryMessage(sessionId, data);
            },
            onError: (sessionId, error) => {
                console.error(`[App] Session ${sessionId} error:`, error.message);
            },
        }
    );

    // 5. Create network server with the same transport
    server = new NetworkServer(transport, runtime, {
        onUnknownInboundOpcode: async (event: UnknownOpcodeEvent) => {
            // Log only - NEVER disconnect (async hook supported)
            console.warn(
                `[App] Unknown opcode ${event.opcode} from session ${event.sessionId}`,
                `(${event.payload.length} bytes) - ignoring`
            );
        },
    });

    // 6. Get typed outbound proxy (uses the same transport)
    const game2cProxy = server.createOutboundProxy<Game2C.Proxy>();

    // 7. Register message handlers on stub
    stub.onLoginRequest(async (sessionId, msg) => {
        console.log(`[Handler] LoginRequest from session ${sessionId}:`, msg);

        // Send response via the same transport
        await game2cProxy.sendLoginResponse(sessionId, {
            success: true,
            playerId: BigInt(sessionId * 1000),
            errorCode: undefined,
            errorMessage: undefined,
        });
    });

    stub.onJoinRoomRequest(async (sessionId, msg) => {
        console.log(`[Handler] JoinRoomRequest from session ${sessionId}:`, msg);

        await game2cProxy.sendJoinRoomResponse(sessionId, {
            success: true,
            roomId: msg.roomId,
            playerIds: [BigInt(sessionId * 1000)],
        });
    });

    stub.onChatMessage(async (sessionId, msg) => {
        console.log(`[Handler] ChatMessage from session ${sessionId}:`, msg);

        // Broadcast to all sessions via the same transport
        const sessionIds = transport.getSessionIds();
        for (const targetId of sessionIds) {
            await game2cProxy.sendChatNotify(targetId, {
                channel: msg.channel,
                senderId: BigInt(sessionId * 1000),
                senderName: `User${sessionId}`,
                message: msg.message,
                timestamp: BigInt(Date.now()),
            });
        }
    });

    stub.onUploadData(async (sessionId, msg) => {
        console.log(`[Handler] UploadData from session ${sessionId}: ${msg.data.length} bytes`);
    });

    // 8. Start transport (single call)
    await transport.start();
    console.log(`[App] Example Network Server running on port ${PORT}`);

    // Graceful shutdown
    process.on('SIGINT', async () => {
        console.log('\n[App] Shutting down...');
        await transport.stop();
        process.exit(0);
    });
}

main().catch(console.error);
