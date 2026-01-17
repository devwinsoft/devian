# BasicWsClient Sample

A minimal sample demonstrating `WebSocketClientBehaviourBase` usage with generated Runtime.

## Architecture

This sample uses the **generated Runtime pattern**:

- **Inbound dispatch** is handled by `C2Sample.Runtime` (generated code)
- **Content logic** is implemented in `C2Sample.Stub` (handler only)
- **Outbound messages** are sent via `C2Sample.Proxy`

No manual opcode switch or envelope parsing is required in sample code.

## Setup

1. Import this sample via Package Manager → Samples
2. Create an empty GameObject
3. Attach `EchoWsClientSample` component
4. Set the WebSocket URL in the inspector
5. Play and use Context Menu → Connect / Disconnect

## Files

- `EchoWsClientSample.cs` - Sample MonoBehaviour extending `WebSocketClientBehaviourBase`, uses generated `C2Sample.Runtime` and `C2Sample.Proxy` for sending
- `SampleProtocolSmokeTest.cs` - Smoke test demonstrating sample protocol type accessibility
- `Devian.Sample.asmdef` - Assembly definition for sample code

## Sample Protocol

This sample references the generated protocol types from `Runtime/Generated.Sample/`:

- `Devian.Network.Sample.C2Sample` - Client-to-server messages (Ping, Echo)
- `Devian.Network.Sample.Sample2C` - Server-to-client messages (Pong, EchoReply)

Each protocol provides:
- `Runtime` - `INetRuntime` implementation for inbound dispatch
- `Stub` - Abstract handler class (inherit and override `On*` methods)
- `Proxy` - Outbound sender (requires `ISender` adapter)

To run the smoke test:
1. Attach `SampleProtocolSmokeTest` to a GameObject
2. Use Context Menu → Run Smoke Test

## Notes

- Samples are copied to `Assets/Samples/` on import - modify freely
- Base class provides no policy (no auto-connect, no reconnect)
- Connection policies should be implemented in your project
- Sample protocol code is in Runtime, not Samples~ (to enable compilation)
