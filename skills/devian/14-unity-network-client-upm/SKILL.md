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

**Serialize Fields:**

| 필드 | 타입 | 설명 |
|------|------|------|
| `Url` | `string` | WebSocket 접속 URL |
| `SubProtocols` | `string[]` | 서브 프로토콜 목록 |
| `AutoConnect` | `bool` | Start()에서 자동 연결 여부 |

**Public Methods (sync):**

```csharp
void Initialize(INetRuntime runtime)
void Connect()
void Close()
void SendFrame(byte[] frame)
```

**Unity Lifecycle:**

- `Start()`: AutoConnect면 Connect 호출
- `Update()`: 내부 `NetWsClient.Update()` 호출 (메인스레드 디스패치 큐 flush)
- `OnDestroy()`: Close/Dispose

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

---

## Reference

- Parent: `skills/devian/12-network-ws-client/SKILL.md`
- Core: `framework-cs/module/Devian/`
