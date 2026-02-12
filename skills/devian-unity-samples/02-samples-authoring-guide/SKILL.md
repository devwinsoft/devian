# 02-samples-authoring-guide

> **UPM Samples~ 정책 엔트리**
>
> **Samples~의 역할:**
> - Samples~는 **폐기가 아니라** UPM 표준 방식으로 templates를 배포하는 메커니즘
> - Templates는 사용자(개발자)가 **Import 후 수정**해서 사용하는 것이 목적
> - Samples~는 "generated artifact"가 아니라 **"editable source distribution"** 성격
>
> **주의사항:**
> - Import된 샘플은 프로젝트 Assets 폴더로 복사됨 (원본은 Packages 내 유지)
> - sync 동작 시 Packages 내 원본만 갱신됨, Assets로 복사된 사용자 수정본은 보존
>
> **세부 문서:**
> - Templates 원본: `framework-cs/upm/com.devian.samples/Samples~/`
> - Samples~ 생성 절차: `skills/devian-unity-samples/03-samples-creation/SKILL.md`
> - Network Sample: `skills/devian-unity-samples/10-samples-network/SKILL.md`
> - LocalSave Manager Sample: `skills/devian-unity-samples/32-samples-localsave-manager/SKILL.md`
> - CloudSave Manager Sample: `skills/devian-unity-samples/33-samples-cloudsave-manager/SKILL.md`

---

Status: ACTIVE  
AppliesTo: v10  
Type: Policy / Requirements

## Prerequisites

**Unity C# 문법 제한:** 샘플 코드는 `skills/devian-core/04-unity-csharp-compat/SKILL.md`를 준수한다 (금지 문법 사용 시 FAIL).

## SSOT

이 문서는 **Devian UPM 샘플 제공 정책/규약**을 정의한다.

**Single Source of Truth:**
- **수동 관리 패키지**: `framework-cs/upm/<packageName>/...` — 수동으로 관리하는 "완벽한 UPM 패키지"
- **생성 패키지**: `framework-cs/upm/<packageName>/...` — 빌드가 생성하는 "완벽한 UPM 패키지" (GitHub URL 배포용)
- **최종 출력**: `framework-cs/apps/UnityExample/Packages/<packageName>` — 빌드 출력물(복사본), 직접 수정 금지

**동기화 흐름:**
```
upm + upm → packageDir (패키지 단위 clean+copy)
```

> 수동 패키지(예: com.devian.foundation)는 upm에서 관리하고,
> 생성 패키지(예: com.devian.domain.common)는 upm에서 관리한다.

---

## 완벽한 UPM 패키지 DoD (Definition of Done)

upm / upm 모두 아래 조건을 만족해야 한다:

| 항목 | 요구사항 |
|------|----------|
| `package.json` | 패키지 루트에 존재, `name` 필드 유효 |
| 폴더명 일치 | 폴더명 == `package.json.name` |
| Runtime/Editor 분리 | Runtime asmdef + Editor asmdef (샘플에 한해) |
| Editor asmdef | `includePlatforms: ["Editor"]` 필수 |
| Samples~ | 존재 시 metadata sync 규칙 준수 |
| using UnityEditor | Runtime 코드에서 금지 |

---

## Unity Sample Authoring Rules (Hard Rules)

### A) 샘플 소스 위치 (Hard Rule)

**Hard Rule:**
샘플 코드는 **반드시** 다음 위치에서만 작성한다:
```
framework-cs/upm/<packageName>/Samples~/...
```

**금지:**
- `framework-cs/apps/UnityExample/Assets/**` 아래에 샘플 스크립트 생성/수정 금지
- `framework-cs/apps/UnityExample/Packages/**` 직접 수정 금지 (빌드 출력물이므로 덮어씌워짐)

### B) Samples~ 샘플 필수 구조: Runtime/Editor 분리 (Hard Rule)

**Hard Rule:**
모든 샘플은 **반드시** `Runtime/`과 `Editor/` 폴더로 분리해야 한다.

**필수 구조:**
```
upm/<packageName>/Samples~/Network/
├── README.md                         ← 샘플 루트에 위치
├── Runtime/
│   ├── [asmdef: Devian.Samples.Network]          ← Runtime asmdef
│   ├── GameNetManager.cs             ← partial 네트워크 매니저 (Stub/Proxy 내부 생성)
│   └── Game2CStub.cs                 ← partial 메시지 스텁 (핸들러 내부 처리)
└── Editor/
    ├── [asmdef: Devian.Samples.Network.Editor]   ← Editor-only asmdef (includePlatforms: ["Editor"])
    └── NetworkSampleMenu.cs          ← 에디터 메뉴
```

**금지:**
- Runtime 코드에 `using UnityEditor;` 사용 금지
- Editor asmdef에 `includePlatforms: []` 사용 금지 (반드시 `["Editor"]` 지정)

### C) Editor asmdef 구성

**Editor-only asmdef:**

```json
// Editor/Devian.Samples.Network.Editor.asmdef
{
    "name": "Devian.Samples.Network.Editor",
    "rootNamespace": "Devian",
    "references": ["Devian.Samples.Network"],
    "includePlatforms": ["Editor"],
    "excludePlatforms": [],
    "allowUnsafeCode": false,
    "overrideReferences": false,
    "precompiledReferences": [],
    "autoReferenced": true,
    "defineConstraints": [],
    "versionDefines": [],
    "noEngineReferences": false
}
```

### D) 에디터 메뉴 (NetworkSampleMenu)

**역할:**
- 메뉴에서 `GameNetManager` GameObject 생성
- 사용법 안내

**메뉴 경로:**
- `Devian/Samples/Network/Create GameNetManager`
- `Devian/Samples/Network/How to Use`

### E) Disconnect 행동 DoD (Hard DoD)

**Hard DoD - Disconnect 후 상태 갱신 필수:**

1. **OnClose 이벤트 호출 필수 (시간 제한):** Disconnect 호출 시 **1초 이내**(또는 1~2 프레임 + 네트워크 지연 허용)에 `OnClose` 이벤트가 발생해야 한다.

2. **IsConnected 갱신 필수:** `IsConnected`가 `false`로 바뀌어야 한다.

**Hard FAIL 조건:**
- "연결은 끊겼는데(IsOpen=false) OnClose가 안 오는 상태"는 **FAIL**
- OnClose 이벤트 없이 IsConnected만 false로 우회하면 **FAIL**
- 1초 후에도 OnClose가 발생하지 않으면 **FAIL**

**구현 금지 (재발 방지):**
- Disconnect는 Close 이벤트를 통해 상태가 갱신되어야 하며, Close 이전에 OnClose 핸들러를 제거하면 **FAIL**

### F) Packages 반영 확인 (Hard Rule)

**샘플 실행 전 필수 체크:**

Disconnect/OnClosed 버그 수정 시, 반드시 `Packages/com.devian.foundation/...`에 반영됐는지 확인한다.

**Hard FAIL 조건:**
- `upm`와 `Packages`의 파일이 다르면 **FAIL** (sync 누락)
- `Packages/`에서 직접 수정한 경우 **정책 위반** (다음 sync에서 손실)

**동기화 누락 발견 시:**
1. 빌더 실행: `node build.js ../../../{buildInputJson}` (예: `node build.js ../../../input/input_common.json`)
2. 또는 수동 sync: `rm -rf Packages/{pkg} && cp -r upm/{pkg} Packages/{pkg}`

---

## 빌드 통합 (Build Integration)

### Builder MUST copy Samples~ (Hard Rule)

**Hard Rule:**
Builder는 **반드시** `Samples~` 폴더를 upm에서 UnityExample/Packages로 복사해야 한다.

- Source에 `Samples~`가 존재하면 Target에도 **반드시** 존재해야 함
- `copyUpmToTarget()` 함수에서 `Samples~` 복사가 `syncSamplesMetadata()` 호출 **전에** 실행되어야 함

### samplePackages 설정

`input/config.json`에 샘플 패키지를 등록:

```json
{
  "upmConfig": {
    "sourceDir": "../framework-cs/upm",
    "packageDir": "../framework-cs/apps/UnityExample/Packages"
  },
  "samplePackages": [
    "com.devian.samples"
  ]
}
```

**upmConfig 필드 정의:**

| 필드 | 의미 |
|------|------|
| `sourceDir` | 수동 관리 UPM 패키지 루트 (upm) |
| `packageDir` | Unity 최종 패키지 루트 (sync 대상) |

**samplePackages 규칙 (Hard Rule):**
- **반드시 문자열 배열**로 정의
- `com.devian.samples`만 허용 — 라이브러리/도메인 패키지는 포함 금지
- `staticUpmPackages` 키는 금지이며 사용 시 빌드 FAIL

**경로 계산 (upmConfig 기반):**
- `sourceDir` = `{upmConfig.sourceDir}/{packageName}` → `../framework-cs/upm/com.devian.samples`
- `targetDir` = `{upmConfig.packageDir}/{packageName}` → `../framework-cs/apps/UnityExample/Packages/com.devian.samples`

**결과:**
빌더가 `upm`을 `packageDir`로 sync하며, `Samples~` 콘텐츠도 포함된다.

---

## GameNetManager Spec (Online-only, TS GameServer)

### 필수 요구사항

| 항목 | 요구사항 |
|------|----------|
| Default URL | `ws://localhost:8080` |
| Offline mode | **NOT supported** (no offline/loopback) |
| Auto-send on connect | **NOT allowed** (no auto-send in OnOpen) |

### Protocol Direction Contract

| 방향 | Protocol | 메시지 |
|------|----------|--------|
| **Outbound** (Client→Server) | `C2Game.Proxy` | Ping, Echo |
| **Inbound** (Server→Client) | `Game2CStub` (partial 클래스로 확장) | Pong, EchoReply |

### 내부 처리 + partial 확장 패턴 (Hard Rule)

**Stub/Proxy는 GameNetManager가 내부에서 생성/보관:**
- `_stub = new Game2CStub()` — 생성자에서 내부 생성
- `_proxy = new C2Game.Proxy(...)` — OnTransportCreated()에서 내부 생성

**사용자 확장은 partial 클래스로:**
```csharp
// Game2CStub.Partial.cs
public partial class Game2CStub
{
    partial void OnPongImpl(Game2C.EnvelopeMeta meta, Game2C.Pong message)
    {
        // Custom handling
    }
}
```

---

## 금지

- `upm` 외부에서 샘플 소스 작성 금지
- `UnityExample/Packages/**` 직접 수정 금지 (빌드 출력물)
- Runtime 코드에 `using UnityEditor` 사용 금지
- Editor asmdef에 `includePlatforms: []` 사용 금지
- **Close 처리에서 이벤트 unhook을 Close 이전에 수행 금지** (Disconnect 상태 갱신 불가 원인)
- **GameNetworkClientSample 파일 생성 금지** — 삭제됨
- **별도 .g.cs 파일 생성 금지** — 단일 파일로 통합
- **BaseGameNetworkClient 사용 금지** — GameNetManager로 대체됨
- **외부 Stub 주입/등록 금지** — RegisterStub(), inboundStub 프로퍼티 사용 금지
- **외부 핸들러 등록 금지** — RegisterHandler() 등 사용 금지, partial 확장으로 처리

---

## Reference

- UPM 소스: `framework-cs/upm/com.devian.foundation/Runtime/Unity/Network/`
- Related: `skills/devian-core/03-ssot/SKILL.md` (Foundation Package SSOT)
