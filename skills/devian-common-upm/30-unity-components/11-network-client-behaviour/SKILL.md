# 11-network-client-behaviour

Status: ACTIVE  
AppliesTo: v10  
Type: Unity Component Policy / API

## Purpose

`com.devian.unity`에 포함된 `NetWsClientBehaviourBase`의 정책/API/확장 지점을 정의한다.  
protobuf 종속 회피를 위한 분리(별도 asmdef) 같은 우회 설계를 하지 않고, Unity에서 단순하게 네트워크 클라이언트를 사용하기 위한 베이스를 제공한다.

---

## Location (SSOT)

정본 소스:
- `framework-cs/upm/com.devian.unity/Runtime/Network/NetWsClientBehaviourBase.cs`

UnityExample 복사본(직접 수정 금지):
- `framework-cs/apps/UnityExample/Packages/com.devian.unity/Runtime/Network/NetWsClientBehaviourBase.cs`

---

## Assembly Policy (Hard Rule)

- `NetWsClientBehaviourBase`는 별도 asmdef로 분리하지 않는다.
- `Devian.Unity.Network.asmdef`는 사용하지 않는다.
- `NetWsClientBehaviourBase`는 `Devian.Unity.Common.asmdef`(Runtime)에 포함된다.

---

## Design Principles (Hard Rule)

- 정책 필드 제공 금지: url 저장, connectOnStart, autoReconnect 같은 "프로젝트 정책"은 베이스에 넣지 않는다.
- Connect 시그니처는 단 하나만 제공한다: `Connect(string url)` (콜백 변형 금지)
- "connected" 같은 프로젝트 전용 콜백 제공 금지
- 최소 엔진만 제공: Connect / Close / TrySend / Update flush
- 확장 포인트는 `virtual hook`로만 제공한다.

---

## Public API

- `bool IsConnected { get; }`
- `void Connect(string url)`
- `void Close()`
- `bool TrySend(ReadOnlySpan<byte> frame)`
- `bool TrySend(byte[] frame)`

---

## Extension Points (Virtual Hooks)

- URL 처리:
  - `NormalizeUrl(string url)`
  - `ValidateUrlOrThrow(Uri uri)`  
    - 기본: ws/wss만 허용
    - WebGL 제한 등 플랫폼 정책은 여기서 처리

- 연결 흐름:
  - `OnConnectRequested(Uri uri)`
  - `OnConnectFailed(string rawUrl, Exception ex)`
  - `OnOpened()`
  - `OnClosed(ushort closeCode, string reason)`
  - `OnClientError(Exception ex)`

- 프레임 처리:
  - `OnParseError(int sessionId, Exception ex)`
  - `OnUnhandledFrame(int sessionId, int opcode, ReadOnlySpan<byte> payload)`

- 클라이언트 구성:
  - `CreateClient(Uri uri, NetClient core)`
  - `HookClientEvents(NetWsClient client)`
  - `UnhookClientEvents(NetWsClient client)`

---

## Unity Lifecycle Policy (Hard Rule)

- `Update()`에서 `_client.Update()`로 디스패치 큐를 메인 스레드에서 flush 해야 한다.
- `OnDestroy()`에서 Dispose 정리한다.
- `Close()`에서 이벤트 unhook을 먼저 하지 않는다. OnClose 이벤트 수신 후 정리하는 흐름을 유지한다.

---

## DoD (Hard Gate)

- [ ] `Devian.Unity.Network.asmdef`가 repo에 존재하지 않는다.
- [ ] `NetWsClientBehaviourBase.cs`가 `Devian.Unity.Common` 런타임 어셈블리에 포함된다.
- [ ] 샘플 asmdef에서 `Devian.Unity.Network` 참조가 0건이다.

---

## Reference

- Related: `skills/devian-common-upm/20-packages/com.devian.unity/SKILL.md`
- Related: `skills/devian/12-network-ws-client/SKILL.md`
