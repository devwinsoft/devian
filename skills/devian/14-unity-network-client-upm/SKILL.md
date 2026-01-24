# 14-unity-network-client-upm

Status: ACTIVE  
AppliesTo: v10

## Prerequisites

**Unity C# 문법 제한:** 이 패키지의 모든 코드는 `skills/devian/04-unity-csharp-compat/SKILL.md`를 준수한다 (금지 문법 사용 시 FAIL).

## SSOT

이 문서는 Unity UPM 패키지 `com.devian.unity.network`의 **정책/구조/규약**을 정의한다.

- 구현 및 공개 API(시그니처 포함)는 **코드가 정답**이며, 문서의 코드는 참고 예시다.
- 코드 변경 시 문서를 SSOT로 맞추지 않는다(필요하면 참고 수준으로 갱신).

---

## 목표

- UnityEngine.dll을 외부 C# 프로젝트에서 직접 참조하지 않는다.
- Unity 프로젝트 내부에서 컴파일되는 UPM(embedded package) 형태로 제공한다.
- 최소 제공 컴포넌트:
  - `Devian.Unity.Network.NetWsClientBehaviourBase` (MonoBehaviour)
- WebGL을 지원한다. (WebSocket.jslib + WebGLWsDriver + NetWsClient 분기)

## 비목표

- TS 변경 없음.
- Devian 런타임 자체를 이 패키지에 복사하지 않는다.
  - 런타임은 `com.devian.core` 패키지로 별도 제공된다.

---

## 패키지 경로

| 유형 | 경로 |
|------|------|
| SSOT (정적 패키지) | `framework-cs/upm-src/com.devian.unity.network/` |
| UnityExample (복사본) | `framework-cs/apps/UnityExample/Packages/com.devian.unity.network/` |

## 패키지 레이아웃

```
com.devian.unity.network/
├── package.json
└── Runtime/
    ├── Devian.Unity.Network.asmdef
    └── NetWsClientBehaviourBase.cs
```

---

## asmdef 규약

| 항목 | 값 |
|------|-----|
| asmdef 이름 | `Devian.Unity.Network` |
| 참조 | `["Devian.Core"]` |
| 플랫폼 제외 | `[]` |

---

## 공개 API (최소)

### `NetWsClientBehaviourBase`

**설계 원칙:**
- 정책 필드(url 저장, autoConnect, reconnect 등)를 제공하지 않는다
- 최소 엔진만 제공: Connect/Close/TrySend/Update
- 서브클래스는 `CreateRuntime()`을 구현하고 필요 시 훅을 오버라이드한다

**Properties:**

| 이름 | 타입 | 설명 |
|------|------|------|
| `IsConnected` | `bool` | 현재 연결 여부 (read-only) |

**Public Methods (sync):**

```csharp
void Connect(string url)      // WebSocket 연결 시작
void Close()                  // 연결 종료
bool TrySend(ReadOnlySpan<byte> frame)  // 프레임 전송 시도
bool TrySend(byte[] frame)    // 프레임 전송 시도 (편의 오버로드)
```

**Abstract (서브클래스 필수 구현):**

```csharp
protected abstract INetRuntime CreateRuntime()
```

**Virtual Hooks (선택적 오버라이드):**

```csharp
protected virtual void OnConnectRequested(Uri uri)
protected virtual void OnConnectFailed(string rawUrl, Exception ex)
protected virtual void OnOpened()
protected virtual void OnClosed(ushort closeCode, string reason)
protected virtual void OnClientError(Exception ex)
protected virtual void OnParseError(int sessionId, Exception ex)
protected virtual void OnUnhandledFrame(int sessionId, int opcode, ReadOnlySpan<byte> payload)
```

**Unity Lifecycle:**

- `Update()`: 내부 `NetWsClient.Update()` 호출 (메인스레드 디스패치 큐 flush)
- `OnDestroy()`: `DisposeClient()` 호출

---

## Hard Rules (Close 이벤트 처리)

**CRITICAL - Close 처리 규칙:**

1. **Close 전에 Unhook 금지:** `CloseInternal()`에서 `UnhookClientEvents()`를 호출하고 나서 `Close()`를 호출하면 OnClose 이벤트가 전달되지 않는다. 재발 방지를 위해 금지.

2. **로컬 Close에서도 OnClosed 호출 필수:** 사용자가 `Close()` 버튼을 눌렀을 때도 `OnClosed(code, reason)` 훅이 호출되어야 한다.

3. **HandleClose에서 DisposeClient 호출:** OnClose 이벤트를 받은 시점(`HandleClose`)에서 `OnClosed()` 호출 후 `DisposeClient()`로 정리한다.

4. **Connect 시작 시 DisposeClient:** 재연결 시에는 `CloseInternal()`이 아닌 `DisposeClient()`로 즉시 정리한다(이전 client의 이벤트가 새 상태를 오염시키지 않도록).

---

## Hard Rules (NetWsClient CTS Cancel)

**로컬 Disconnect(클라이언트 Close 호출)에서도 OnClosed가 반드시 호출되어야 한다.**

- non-WebGL에서는 **Close 시 CancellationTokenSource.Cancel()**로 RecvLoop 종료를 보장해야 한다.
- `_closeRequested` 플래그로 정상 종료와 비정상 종료를 구분한다.
- RecvLoop의 OperationCanceledException은 `when (_closeRequested)` 조건으로 정상 종료 처리한다.

**Update 호출 필수:**
- `NetWsClient.Update()`가 dispatch queue를 flush 하므로, BehaviourBase는 **매 프레임** `_client.Update()`를 호출해야 한다.
- Derived가 Update를 override하면 `base.Update()` 호출 필수.

---

## 의존성 (중요)

이 패키지는 `com.devian.core` 패키지가 Unity 프로젝트에 존재해야 컴파일된다.

`com.devian.core` 패키지는 단일 어셈블리(`Devian.Core`)를 제공한다:
- namespace: `Devian` (런타임 단일 네임스페이스)
- 네트워크 타입은 `Net` 접두사를 사용한다 (예: `NetClient`, `NetWsClient`, `NetHttpRpcClient`)

`com.devian.unity.network`는 `Devian.Core` asmdef를 참조한다.

**package.json dependencies:**

```json
{
  "dependencies": {
    "com.devian.core": "0.1.0",
    "com.devian.module.common": "0.1.0"
  }
}
```

---

## 금지

- UnityEngine.dll을 직접 참조해 DLL 빌드하는 방식 금지.
- WebGL 재연결/백오프/성능 최적화 정책은 이 SKILL에서 강제하지 않는다.
- **Close 처리에서 이벤트 unhook을 Close 이전에 수행 금지** (재발 방지)

---

## Reference

- Parent: `skills/devian/12-network-ws-client/SKILL.md`
- Core: `framework-cs/module/Devian/`
