# Unity WebSocket Client Sample 생성 체크리스트

Status: ACTIVE  
Type: Implementation Checklist

---

## 이 문서의 목적

Claude가 Unity WS Client Sample 생성 시 **빠뜨리는 항목을 방지**하기 위한 체크리스트.

---

## 생성해야 하는 파일 목록 (6개)

```
Samples~/BasicWsClient/
├── Devian.Sample.asmdef              [1]
├── EchoWsClientSample.cs             [2]
├── SampleProtocolSmokeTest.cs        [3]
├── README.md                         [4]
└── Editor/
    ├── Devian.Sample.Editor.asmdef   [5]
    └── EchoWsClientSampleEditor.cs   [6]
```

---

## 파일별 체크리스트

### [1] Devian.Sample.asmdef

| 항목 | 값 |
|------|-----|
| name | `"Devian.Sample"` |
| rootNamespace | `"Devian.Sample"` |
| references | `["Devian.Core", "Devian.Network", "Devian.Unity.Network", "Devian.Network.Sample"]` |
| includePlatforms | `[]` (빈 배열 = 모든 플랫폼) |

### [2] EchoWsClientSample.cs

| 체크 | 항목 |
|------|------|
| □ | `namespace Devian.Sample` |
| □ | `public bool IsConnected { get; private set; }` ← **Editor에서 접근 필요** |
| □ | `public void ConnectWithInspectorUrl()` ← **버튼용** |
| □ | `public new void Disconnect()` ← **버튼용** |
| □ | `public void SendPing()` ← **버튼용** |
| □ | `public void SendEcho()` ← **버튼용** |
| □ | `[SerializeField] private string url = "ws://localhost:8080";` |
| □ | `OnOpened()`에 `// NO auto-send` 주석 |
| □ | `[ContextMenu]` 사용 안 함 |
| □ | `using UnityEditor;` 없음 |

### [5] Editor/Devian.Sample.Editor.asmdef

| 항목 | 값 |
|------|-----|
| name | `"Devian.Sample.Editor"` |
| rootNamespace | `"Devian.Sample.Editor"` |
| references | `["Devian.Sample"]` |
| **includePlatforms** | `["Editor"]` ← **반드시 Editor만** |

### [6] Editor/EchoWsClientSampleEditor.cs

| 체크 | 항목 |
|------|------|
| □ | `using UnityEditor;` |
| □ | `namespace Devian.Sample.Editor` |
| □ | `[CustomEditor(typeof(EchoWsClientSample))]` |
| □ | `public class ... : UnityEditor.Editor` |
| □ | `public override void OnInspectorGUI()` |
| □ | `DrawDefaultInspector();` 호출 |
| □ | `var sample = (EchoWsClientSample)target;` |
| □ | `GUILayout.Button("Connect")` |
| □ | `GUILayout.Button("Disconnect")` |
| □ | `GUILayout.Button("Send Ping")` |
| □ | `GUILayout.Button("Send Echo")` |
| □ | Ping/Echo 버튼에 `EditorGUI.BeginDisabledGroup(!Application.isPlaying || !sample.IsConnected)` |
| □ | `EditorGUILayout.LabelField("Connected:", sample.IsConnected ? "Yes" : "No")` |

---

## CustomEditor 코드 템플릿 (복사용)

```csharp
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

---

## Editor asmdef 템플릿 (복사용)

```json
{
    "name": "Devian.Sample.Editor",
    "rootNamespace": "Devian.Sample.Editor",
    "references": [
        "Devian.Sample"
    ],
    "includePlatforms": [
        "Editor"
    ],
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

---

## 금지 패턴

| 금지 | 대신 사용 |
|------|----------|
| `[ContextMenu("Connect")]` | CustomEditor 버튼 |
| `wss://localhost/ws` | `ws://localhost:8080` |
| `OnOpened()` 안에서 자동 전송 | 버튼으로만 전송 |
| Runtime에서 `using UnityEditor` | Editor/ 폴더에서만 |
| `includePlatforms: []` (Editor asmdef) | `includePlatforms: ["Editor"]` |

---

## 듀얼 업데이트 (잊지 말 것)

Samples~ 수정 후 반드시 Assets/Samples/에도 복사:

```bash
cp -r "Packages/.../Samples~/BasicWsClient/"* "Assets/Samples/Devian Unity Network/0.1.0/Basic Ws Client/"
```

---

## 생성 완료 검증

```bash
# Editor 폴더 존재
[ -d "Editor" ] && echo "✅"

# Editor asmdef에 includePlatforms: Editor
grep -q '"Editor"' Editor/*.asmdef && echo "✅"

# ContextMenu 없음
[ $(grep -c "ContextMenu" *.cs) -eq 0 ] && echo "✅"

# CustomEditor 어트리뷰트 존재
grep -q "CustomEditor" Editor/*.cs && echo "✅"

# 4개 버튼 존재
[ $(grep -c "GUILayout.Button" Editor/*.cs) -eq 4 ] && echo "✅"

# IsConnected 프로퍼티 존재
grep -q "IsConnected" *.cs && echo "✅"
```
