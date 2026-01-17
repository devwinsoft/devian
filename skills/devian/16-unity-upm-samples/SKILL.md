# 16-unity-upm-samples

Status: ACTIVE  
AppliesTo: v10  
Type: Policy / Requirements

## SSOT

이 문서는 **Devian UPM 샘플 제공 정책/규약**을 정의한다.

- 구현 및 공개 API는 **코드가 정답**이며, 문서의 코드/시그니처는 참고 예시다.
- 코드 변경 시 문서를 SSOT로 맞추지 않는다(필요하면 참고 수준으로 갱신).

---

## 샘플 제공 위치

```
framework-cs/apps/UnityExample/Packages/com.devian.unity.network/
├── Runtime/
│   └── Generated.Sample/                    ← 샘플 프로토콜 생성 코드
│       ├── Devian.Unity.Network.Sample.asmdef
│       └── Devian.Network.Sample/
│           ├── C2Sample.g.cs
│           └── Sample2C.g.cs
└── Samples~/
    └── BasicWsClient/
        ├── Devian.Sample.asmdef
        ├── EchoRuntime.cs
        ├── EchoWsClientSample.cs
        ├── SampleProtocolSmokeTest.cs       ← 샘플 프로토콜 참조 예제
        └── README.md
```

- UPM `Samples~` 폴더는 Unity Package Manager에서 "Import into Project" 시 `Assets/Samples/` 하위로 복사된다.
- 복사된 샘플은 **사용자가 자유롭게 수정**하는 용도다.
- **주의**: `Samples~`는 Unity 숨김 폴더 규약(`~` 접미사)이므로 Project 뷰에 표시되지 않을 수 있다. OS 탐색기나 터미널에서 확인한다.

---

## package.json samples 메타데이터 (필수)

`Samples~` 폴더가 존재하는 패키지는 **반드시** `package.json`에 `samples` 배열을 선언해야 Unity Package Manager에서 샘플 설치 UI가 표시된다.

### 규칙

- `samples[].path`는 `Samples~/...` 하위 상대경로이며 **폴더명과 대소문자까지 정확히 일치**해야 한다.
- `samples[].displayName`은 필수 (UI에 표시될 이름)
- `samples[].description`은 권장 (UI에 표시될 설명)

### 예시 (`com.devian.unity.network/package.json`)

```json
{
  "name": "com.devian.unity.network",
  "version": "0.1.0",
  "displayName": "Devian Unity Network",
  "description": "Unity adapter for Devian.Network (MonoBehaviours).",
  "unity": "2021.3",
  "author": {
    "name": "Kim, Hyong Joon"
  },
  "dependencies": {
    "com.devian.network": "0.1.0"
  },
  "samples": [
    {
      "displayName": "Basic Ws Client",
      "description": "Minimal WebSocket usage sample.",
      "path": "Samples~/BasicWsClient"
    }
  ]
}
```

---

## 샘플 네임스페이스 규약

모든 샘플 코드는 다음 네임스페이스를 사용한다:

```csharp
namespace Devian.Sample
{
    // ...
}
```

- `Devian.Unity.Network` (Runtime)와 구분하기 위함
- Import 후 사용자가 네임스페이스를 변경해도 무방함

---

## Base 클래스 정책 (중요)

`WebSocketClientBehaviourBase`는 **정책을 강제하지 않는다**.

### Base가 제공하지 않는 것 (금지)

| 항목 | 설명 |
|------|------|
| `url` 필드 저장 | URL은 `Connect(string url)` 호출 시에만 사용 |
| `connectOnStart` | 자동 연결은 프로젝트 정책 |
| `autoReconnect` | 재연결 로직은 프로젝트 정책 |
| `onConnected` 콜백 | "연결 완료"의 정의는 프로젝트마다 다름 |

### Base가 제공하는 것 (최소 엔진)

| API | 설명 |
|-----|------|
| `Connect(string url)` | 연결 시도 (콜백 없음) |
| `Close()` | 연결 종료 |
| `TrySend(ReadOnlySpan<byte>)` | 프레임 전송 |
| `Update()` | 메인 스레드 디스패치 큐 처리 |
| `CreateRuntime()` (abstract) | Runtime 생성 (서브클래스 구현) |

### Base가 제공하는 Virtual Hooks

| Hook | 용도 |
|------|------|
| `NormalizeUrl(string)` | URL 정규화 (기본: trim) |
| `ValidateUrlOrThrow(Uri)` | URL 검증 (기본: ws/wss 스킴만 허용) |
| `OnConnectRequested(Uri)` | 연결 시도 직전 알림 |
| `OnConnectFailed(string, Exception)` | 연결 실패 알림 |
| `OnOpened()` | 연결 성공 알림 |
| `OnClosed(ushort, string)` | 연결 종료 알림 |
| `OnClientError(Exception)` | 전송 오류 알림 |

---

## 연결 정책은 프로젝트에서 구현

다음은 **Base/샘플이 제공하지 않으며**, 프로젝트 레벨에서 구현해야 한다:

- 버전별 URL 선택 로직
- IPv4/IPv6 폴백 전략
- 인증/세션 확정 로직
- 재연결 백오프 전략
- 연결 완료 판정 기준

---

## 샘플 사용 예시

```csharp
// EchoWsClientSample.cs (Samples~에서 제공)
public class EchoWsClientSample : WebSocketClientBehaviourBase
{
    [SerializeField] private string url = "wss://localhost/ws";

    [ContextMenu("Connect")]
    public void ConnectWithInspectorUrl()
    {
        Connect(url);  // 콜백 없이 호출
    }

    protected override INetRuntime CreateRuntime()
    {
        return new EchoRuntime();
    }

    protected override void OnOpened()
    {
        Debug.Log("Connected!");
    }

    protected override void OnClosed(ushort code, string reason)
    {
        Debug.Log($"Closed: {code} {reason}");
    }
}
```

---

## 금지

- Base에 `url` 필드, `connectOnStart`, `autoReconnect`, `Action<...> onConnected` 콜백 추가 금지
- 샘플을 "프레임워크급"으로 확장 금지 (Echo/Connect/Send 수준만)
- `Devian.Network` 모듈(코어) 수정 금지
- **새 샘플 UPM 패키지 추가 금지** (네트워크 샘플은 `com.devian.unity.network`만 확장)

---

## 온전 테스트 가능 샘플 규칙

샘플이 "온전 컴파일/테스트 가능"을 목표로 하는 경우:

### 생성 코드/데이터 위치 (Hard Rule)

샘플이 참조하는 generated/data 파일은 **Samples~ 안에 두지 않는다**. Runtime 하위에 위치해야 한다:

```
com.devian.unity.network/
├── Runtime/
│   ├── Generated.Sample/          ← 샘플 프로토콜 생성 코드
│   │   └── Devian.Network.Sample/
│   │       ├── C2Sample.g.cs
│   │       └── Sample2C.g.cs
│   └── SampleData/                 ← 샘플 데이터 (ndjson 등)
│       └── json/
└── Samples~/
    └── BasicWsClient/
        ├── EchoWsClientSample.cs
        └── SampleProtocolSmokeTest.cs  ← Runtime의 generated를 참조
```

### 이유

1. Samples~는 Import 시 Assets/Samples/로 복사됨
2. 생성 코드가 Samples~ 안에 있으면 참조 해결이 안 됨
3. Runtime에 두면 패키지 설치 즉시 컴파일 가능

### asmdef 설정

샘플 코드가 Runtime의 생성 코드를 참조하려면 asmdef에 references 추가:

```json
{
  "name": "Devian.Sample",
  "references": [
    "Devian.Core",
    "Devian.Network",
    "Devian.Unity.Network",
    "Devian.Unity.Network.Sample"  // ← Generated.Sample의 asmdef
  ]
}
```

---

## Reference

- Base 클래스: `com.devian.unity.network/Runtime/WebSocketClientBehaviourBase.cs`
- 샘플: `com.devian.unity.network/Samples~/BasicWsClient/`
- Related: `skills/devian/14-unity-network-client-upm/SKILL.md`
