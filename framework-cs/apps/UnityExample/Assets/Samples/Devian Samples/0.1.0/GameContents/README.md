# GameContents (Devian Samples)

This sample bundles the following sub-samples in one import:

- Network (GameNetManager + Game2CStub)

Importing `GameContents` installs all sub-codes together under this folder.

## Network

Unity WebSocket client sample using `Devian.Protocol.Game` with Tick-based pump.

### Requirements

- `com.devian.foundation` - Core network infrastructure (INetSession, INetConnector, NetWsConnector)
- `com.devian.protocol.game` - Game protocol (C2Game/Game2C)

### Quick Start

1. Add `GameNetManager` component to a GameObject
2. Call `Connect(url)` to establish connection
3. Use `GameNetManager.Proxy` to send messages
4. Extend via partial class (`Game2CStub.Partial.cs`) for custom handling
