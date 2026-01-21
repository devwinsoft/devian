# BasicWsClient Sample

Online-only WebSocket client sample for TS SampleServer.

## Prerequisites

- TS SampleServer running on `ws://localhost:8080`
- Start server: `cd framework-ts/apps/SampleServer && npm start`

## Usage

1. Attach `EchoWsClientSample` to any GameObject
2. Enter Play mode
3. Use Inspector buttons:
   - **Connect** - Connect to server
   - **Disconnect** - Close connection
   - **Send Ping** - Send Ping message (requires connection)
   - **Send Echo** - Send Echo message (requires connection)

## Protocol Direction

| Direction | Protocol | Messages |
|-----------|----------|----------|
| Client → Server | `C2Sample.Proxy` | Ping, Echo |
| Server → Client | `Sample2C.Runtime` | Pong, EchoReply |

## Notes

- **No offline mode** - Requires running server
- **No auto-send** - Use Inspector buttons to send messages
