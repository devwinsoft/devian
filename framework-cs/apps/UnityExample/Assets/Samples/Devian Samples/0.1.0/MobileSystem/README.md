# MobileSystem (Devian Samples)


This sample bundles the following sub-samples in one import:


- FirebaseManager
- SaveSystem
- AccountManager
- Game network wiring (`GameNetManager`, `Game2CStub`, `ClientSessionHost`)


Importing `MobileSystem` installs all sub-codes together under this folder.

## VirtualGamepad (recycled)

VirtualPad sample is not used here. MobileSystem uses the `Runtime/VirtualGamepad` folder in this sample.

## Network structure

`Runtime/Net` uses the normalized session-host structure together with generated protocol support:

- `GameNetManager.cs`
  - Unity facade
  - owns `Game2CStub`, `C2Game.Proxy`, `ClientSessionHost`
  - forwards `Connect`, `Disconnect`, `Update`, and connection events
- `Game2CStub.cs`
  - concrete inbound stub
  - extend handler behavior via partial methods

The generated session host lives in the protocol package:

- `com.devian.protocol.game/Runtime/Generated/ClientSessionHost.g.cs`
  - generated protocol-group session host
  - inherits lifecycle from `NetClientSessionHost`
  - pairs `Game2C.Runtime` with `C2Game.Proxy`

The generated `C2Game.Proxy` is treated as send-only:

- `AttachSession()`
- `DetachSession()`
- `SendXxx(...)`

It should not own `Connect`, `Tick`, `Disconnect`, or connection state in this sample path.
