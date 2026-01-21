# 16-unity-upm-samples

Status: ACTIVE  
AppliesTo: v10  
Type: Policy / Requirements

## SSOT

이 문서는 **Devian UPM 샘플 제공 정책/규약**을 정의한다.

**Single Source of Truth:**
- UPM 패키지 콘텐츠(샘플 포함)의 권위 있는 소스: `framework-cs/upm-src/<packageName>/...`
- `UnityExample/Packages/<packageName>`은 빌드 출력물(복사본)이며 소스가 아니다.

---

## Unity Sample Authoring Rules (Hard Rules)

### A) 샘플 소스 위치 (Hard Rule)

**Hard Rule:**
샘플 코드는 **반드시** 다음 위치에서만 작성한다:
```
framework-cs/upm-src/<packageName>/Samples~/...
```

**금지:**
- `framework-cs/apps/UnityExample/Assets/**` 아래에 샘플 스크립트 생성/수정 금지
- `framework-cs/apps/UnityExample/Packages/**` 직접 수정 금지 (빌드 출력물이므로 덮어씌워짐)

### B) Samples~ 샘플 필수 구조: Runtime/Editor 분리 (Hard Rule)

**Hard Rule:**
모든 샘플은 **반드시** `Runtime/`과 `Editor/` 폴더로 분리해야 한다.

**필수 구조:**
```
upm-src/<packageName>/Samples~/BasicWsClient/
├── README.md                         ← 샘플 루트에 위치
├── Runtime/
│   ├── Devian.Sample.asmdef          ← Runtime asmdef
│   ├── EchoWsClientSample.cs         ← Runtime 스크립트
│   └── (optional) SampleProtocolSmokeTest.cs
└── Editor/
    ├── Devian.Sample.Editor.asmdef   ← Editor-only asmdef (includePlatforms: ["Editor"])
    └── EchoWsClientSampleEditor.cs   ← Custom Inspector
```

**금지:**
- Runtime 코드에 `using UnityEditor;` 사용 금지
- Editor asmdef에 `includePlatforms: []` 사용 금지 (반드시 `["Editor"]` 지정)

### C) CustomEditor 구현 가이드

**Step 1: Runtime 클래스에서 Editor용 Public API 노출**

```csharp
// Runtime/EchoWsClientSample.cs
public class EchoWsClientSample : WebSocketClientBehaviourBase
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
// Editor/Devian.Sample.Editor.asmdef
{
    "name": "Devian.Sample.Editor",
    "rootNamespace": "Devian.Sample.Editor",
    "references": ["Devian.Sample", "Devian.Unity.Network"],
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

namespace Devian.Sample.Editor
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

---

## 빌드 통합 (Build Integration)

### Builder MUST copy Samples~ (Hard Rule)

**Hard Rule:**
Builder는 **반드시** `Samples~` 폴더를 upm-src에서 UnityExample/Packages로 복사해야 한다.

- Source에 `Samples~`가 존재하면 Target에도 **반드시** 존재해야 함
- `copyUpmToTarget()` 함수에서 `Samples~` 복사가 `syncSamplesMetadata()` 호출 **전에** 실행되어야 함

### staticUpmPackages 설정

`input/build.json`에 UPM 패키지를 등록:

```json
{
  "upmConfig": {
    "sourceDir": "../framework-cs/upm-src",
    "packageDir": "../framework-cs/apps/UnityExample/Packages"
  },
  "staticUpmPackages": [
    { "upmName": "com.devian.unity.network" }
  ]
}
```

**경로 계산 (upmConfig 기반):**
- `sourceDir` = `{upmConfig.sourceDir}/{upmName}` → `../framework-cs/upm-src/com.devian.unity.network`
- `targetDir` = `{upmConfig.packageDir}/{upmName}` → `../framework-cs/apps/UnityExample/Packages/com.devian.unity.network`

**결과:**
빌더가 `UnityExample/Packages/com.devian.unity.network/...`를 생성하며, `Samples~` 콘텐츠도 포함된다.

---

## EchoWsClientSample Spec (Online-only, TS SampleServer)

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
| **Outbound** (Client→Server) | `C2Sample.Proxy` | Ping, Echo |
| **Inbound** (Server→Client) | `Sample2C.Runtime` + `Sample2C.Stub` | Pong, EchoReply |

---

## 금지

- `upm-src` 외부에서 샘플 소스 작성 금지
- `UnityExample/Packages/**` 직접 수정 금지 (빌드 출력물)
- Runtime 코드에 `using UnityEditor` 사용 금지
- Editor asmdef에 `includePlatforms: []` 사용 금지
- EchoWsClientSample에 offline/loopback 모드 추가 금지

---

## Reference

- UPM 소스: `framework-cs/upm-src/com.devian.unity.network/`
- Related: `skills/devian/14-unity-network-client-upm/SKILL.md`
