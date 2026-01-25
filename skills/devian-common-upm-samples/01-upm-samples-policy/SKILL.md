# 03-upm-samples-policy

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
> - Network Template: `skills/devian-common-upm-samples/10-samples-network/SKILL.md`

---

Status: ACTIVE  
AppliesTo: v10  
Type: Policy / Requirements

## Prerequisites

**Unity C# 문법 제한:** 샘플 코드는 `skills/devian/04-unity-csharp-compat/SKILL.md`를 준수한다 (금지 문법 사용 시 FAIL).

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

> 수동 패키지(예: com.devian.unity)는 upm에서 관리하고,  
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
upm/<packageName>/Samples~/BasicWsClient/
├── README.md                         ← 샘플 루트에 위치
├── Runtime/
│   ├── `[asmdef: Devian` + `.Templates.Network]`          ← Runtime asmdef
│   ├── EchoWsClientSample.cs         ← Runtime 스크립트
│   └── (optional) SampleProtocolSmokeTest.cs
└── Editor/
    ├── `[asmdef: Devian` + `.Templates.Network.Editor]`   ← Editor-only asmdef (includePlatforms: ["Editor"])
    └── EchoWsClientSampleEditor.cs   ← Custom Inspector
```

**금지:**
- Runtime 코드에 `using UnityEditor;` 사용 금지
- Editor asmdef에 `includePlatforms: []` 사용 금지 (반드시 `["Editor"]` 지정)

### C) CustomEditor 구현 가이드

**Step 1: Runtime 클래스에서 Editor용 Public API 노출**

```csharp
// Runtime/EchoWsClientSample.cs
public class EchoWsClientSample : NetWsClientBehaviourBase
{
    // ★ Editor에서 연결 상태 확인용 - 반드시 public
    public bool IsConnected { get; private set; }
    
    // ★ CustomEditor 버튼에서 호출할 public 메서드들
    public void ConnectWithInspectorUrl() => Connect(url);
    public new void Disconnect() => Close();
    public void SendPing() { /* ... */ }
    public void SendEcho() { /* ... */ }
}
```

**Step 2: Editor-only asmdef**

```json
// Editor/`[asmdef: Devian` + `.Templates.Network.Editor]`
{
    "name": "<어셈블리명: Devian + .Templates.Network.Editor>",
    "rootNamespace": "Devian",
    "references": ["<어셈블리: Devian + .Templates.Network>", "<어셈블리: Devian + .Unity.Network>"],
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

**Step 3: CustomEditor 클래스**

```csharp
// Editor/EchoWsClientSampleEditor.cs
using UnityEngine;
using UnityEditor;

namespace Devian
{
    [CustomEditor(typeof(EchoWsClientSample))]
    public class EchoWsClientSampleEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            var sample = (EchoWsClientSample)target;

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Actions", EditorStyles.boldLabel);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Connect")) sample.ConnectWithInspectorUrl();
            if (GUILayout.Button("Disconnect")) sample.Disconnect();
            EditorGUILayout.EndHorizontal();

            EditorGUI.BeginDisabledGroup(!Application.isPlaying || !sample.IsConnected);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Send Ping")) sample.SendPing();
            if (GUILayout.Button("Send Echo")) sample.SendEcho();
            EditorGUILayout.EndHorizontal();
            EditorGUI.EndDisabledGroup();

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Status", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Connected:", sample.IsConnected ? "Yes" : "No");
        }
    }
}
```

### D) 패킷 테스트 샘플 필수 Inspector 버튼 (Hard Rule)

**Hard Rule:**
패킷 테스트 샘플은 **반드시** CustomEditor로 구현된 Inspector 버튼을 제공해야 한다:
- **Connect** - 연결
- **Disconnect** - 연결 해제
- **Send Ping** - Ping 메시지 전송
- **Send Echo** - Echo 메시지 전송

**Definition of Done:**
UnityExample Inspector에서 버튼이 표시되지 않으면 **FAIL**이다.

### E) Disconnect 행동 DoD (Hard DoD)

**Hard DoD - Disconnect 후 상태 갱신 필수:**

1. **OnClosed 호출 필수 (시간 제한):** Inspector에서 Disconnect 버튼을 누르면 **1초 이내**(또는 1~2 프레임 + 네트워크 지연 허용)에 샘플의 `OnClosed(code, reason)` 훅이 호출되어야 한다.

2. **IsConnected 갱신 필수:** 샘플의 `IsConnected`가 `false`로 바뀌어 Inspector에 표시되어야 한다.

3. **Send 버튼 비활성화:** `IsConnected == false` 상태에서는 Send 버튼이 비활성화(또는 경고 출력)되어야 한다.

**Hard FAIL 조건:**
- "연결은 끊겼는데(IsOpen=false) OnClosed가 안 오는 상태"는 **FAIL**
- OnClosed 로그 없이 IsConnected만 false로 우회하면 **FAIL**
- 1초 후에도 OnClosed가 발생하지 않으면 **FAIL**

**검증 방법:**
- Play 모드에서 Connect 후 Disconnect 클릭
- Console에서 `OnClosed` 로그가 1초 이내에 출력되어야 PASS
- Inspector의 "Connected: Yes"가 "Connected: No"로 변경되어야 PASS
- "Connected: No" 상태에서 Send 버튼이 disabled(회색)여야 PASS

**구현 금지 (재발 방지):**
- Disconnect는 Close 이벤트를 통해 상태가 갱신되어야 하며, Close 이전에 OnClose 핸들러를 제거하면 **FAIL**
- 샘플에서 `IsConnected = false`를 직접 설정하여 우회하면 **FAIL** (반드시 OnClosed 훅을 통해 갱신)

### F) Packages 반영 확인 (Hard Rule)

**샘플 실행 전 필수 체크:**

Disconnect/OnClosed 버그 수정 시, 반드시 `Packages/com.devian.unity/Runtime/Network/...`에 반영됐는지 확인한다.

**확인 방법:**
```bash
# NetWsClientBehaviourBase.cs 파일 크기/날짜 비교
ls -la upm/com.devian.unity/Runtime/Network/NetWsClientBehaviourBase.cs
ls -la Packages/com.devian.unity/Runtime/Network/NetWsClientBehaviourBase.cs

# MD5 해시 비교 (동일해야 함)
md5sum upm/com.devian.unity/Runtime/Network/NetWsClientBehaviourBase.cs
md5sum Packages/com.devian.unity/Runtime/Network/NetWsClientBehaviourBase.cs
```

**Hard FAIL 조건:**
- `upm` (또는 `upm`)와 `Packages`의 파일이 다르면 **FAIL** (sync 누락)
- `Packages/`에서 직접 수정한 경우 **정책 위반** (다음 sync에서 손실)

**동기화 누락 발견 시:**
1. 빌더 실행: `node build.js ../../../input/input_common.json`
2. 또는 수동 sync: `rm -rf Packages/{pkg} && cp -r upm/{pkg} Packages/{pkg}`

---

## 빌드 통합 (Build Integration)

### Builder MUST copy Samples~ (Hard Rule)

**Hard Rule:**
Builder는 **반드시** `Samples~` 폴더를 upm에서 UnityExample/Packages로 복사해야 한다.

- Source에 `Samples~`가 존재하면 Target에도 **반드시** 존재해야 함
- `copyUpmToTarget()` 함수에서 `Samples~` 복사가 `syncSamplesMetadata()` 호출 **전에** 실행되어야 함

### staticUpmPackages 설정

`input/input_common.json`에 UPM 패키지를 등록:

```json
{
  "upmConfig": {
    "sourceDir": "../framework-cs/upm",
    // removed,
    "packageDir": "../framework-cs/apps/UnityExample/Packages"
  },
  "staticUpmPackages": [
    "com.devian.unity",
    "com.devian.unity"
  ]
}
```

**upmConfig 필드 정의:**

| 필드 | 의미 |
|------|------|
| `sourceDir` | 수동 관리 UPM 패키지 루트 (upm) |
| `packageDir` | Unity 최종 패키지 루트 (sync 대상) |

**staticUpmPackages 형식:**
- **반드시 문자열 배열**로 정의 (객체 형태 `{ "upmName": "..." }` 사용 시 빌드 실패)
- 배열 내 각 문자열은 `upmConfig.sourceDir`에 존재하는 패키지명

**경로 계산 (upmConfig 기반):**
- `sourceDir` = `{upmConfig.sourceDir}/{packageName}` → `../framework-cs/upm/com.devian.unity`
- `targetDir` = `{upmConfig.packageDir}/{packageName}` → `../framework-cs/apps/UnityExample/Packages/com.devian.unity`

**결과:**
빌더가 `upm + upm`을 `packageDir`로 sync하며, `Samples~` 콘텐츠도 포함된다.

---

## EchoWsClientSample Spec (Online-only, TS GameServer)

### 필수 요구사항

| 항목 | 요구사항 |
|------|----------|
| Default URL | `ws://localhost:8080` |
| Offline mode | **NOT supported** (no offline/loopback) |
| Auto-send on connect | **NOT allowed** (no auto-send in OnOpened) |
| Message trigger | **Inspector buttons only** (Connect/Disconnect/Ping/Echo) |

### Protocol Direction Contract

| 방향 | Protocol | 메시지 |
|------|----------|--------|
| **Outbound** (Client→Server) | `C2Game.Proxy` | Ping, Echo |
| **Inbound** (Server→Client) | `Game2C.Runtime` + `Game2C.Stub` | Pong, EchoReply |

---

## 금지

- `upm` 외부에서 샘플 소스 작성 금지
- `UnityExample/Packages/**` 직접 수정 금지 (빌드 출력물)
- Runtime 코드에 `using UnityEditor` 사용 금지
- Editor asmdef에 `includePlatforms: []` 사용 금지
- EchoWsClientSample에 offline/loopback 모드 추가 금지
- **Close 처리에서 이벤트 unhook을 Close 이전에 수행 금지** (Disconnect 상태 갱신 불가 원인)

---

## Reference

- UPM 소스: `framework-cs/upm/com.devian.unity/Runtime/Network/`
- Related: `skills/devian-common-upm/20-packages/com.devian.unity/SKILL.md`
