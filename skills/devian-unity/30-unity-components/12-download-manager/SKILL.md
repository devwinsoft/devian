# 12-download-manager

Status: ACTIVE  
AppliesTo: v10

## SSOT

이 문서는 `DownloadManager` 컴포넌트의 **API, 정책, 검증 규칙**을 정의한다.

---

## 목적/범위

**Addressables Label 기반 패치/다운로드를 제공하는 Unity 전용 컴포넌트.**

- **CompoSingleton**: Bootstrap에서 생성/등록되거나 씬에 배치해야 함
- **인스펙터**: `patchLabels` (Label 리스트) 설정 가능
- **PatchProc**: 라벨별 다운로드 필요 용량 계산
- **DownloadProc**: 라벨별 의존 번들 다운로드 (가중치 기반 진행률)
- **실패 처리**: `onError` 콜백 반드시 호출 ("조용히 종료" 금지)

---

## 소스 경로

| 위치 | 경로 |
|------|------|
| UPM 소스 | `framework-cs/upm/com.devian.foundation/Runtime/Unity/AssetManager/DownloadManager.cs` |
| UnityExample | `framework-cs/apps/UnityExample/Packages/com.devian.foundation/Runtime/Unity/AssetManager/DownloadManager.cs` (derived output) |

---

## Prerequisites (필수 의존성)

**DownloadManager는 Unity Addressables 기반이므로 다음 의존성이 필수:**

### package.json

```json
{
  "dependencies": {
    "com.devian.foundation": "0.1.0",
    "com.unity.addressables": "2.7.6"
  }
}
```

### Devian.Unity.asmdef

```json
{
  "references": [
    "Devian.Core",
    "Unity.Addressables",
    "Unity.ResourceManager"
  ]
}
```

> **주의**: `Unity.Addressables`와 `Unity.ResourceManager` 참조가 없으면 CS0234 네임스페이스 에러 발생.

---

## 인스펙터 필드

| 필드 | 타입 | 기본값 | 설명 |
|------|------|--------|------|
| `patchLabels` | `List<string>` | `[]` | Addressables Labels (다운로드 대상) |
| `forceClearDependencyCache` | `bool` | `false` | Size 계산 전 캐시 삭제 (운영 위험, 테스트용) |

> **주의**: `forceClearDependencyCache`를 `true`로 설정하면 매번 캐시를 삭제하므로 운영 환경에서 사용 금지.

---

## API

### PatchInfo

```csharp
namespace Devian
{
    public sealed class PatchInfo
    {
        /// <summary>
        /// 총 다운로드 크기 (bytes)
        /// </summary>
        public long TotalSize { get; }
        
        /// <summary>
        /// 라벨별 다운로드 크기 (bytes)
        /// </summary>
        public IReadOnlyDictionary<string, long> LabelSizes { get; }
    }
}
```

### DownloadManager

```csharp
namespace Devian
{
    public sealed class DownloadManager : ResSingleton<DownloadManager>
    {
        // ====================================================================
        // Inspector Fields (Serialized)
        // ====================================================================
        
        [SerializeField] private List<string> patchLabels;
        [SerializeField] private bool forceClearDependencyCache;
        
        // ====================================================================
        // Events
        // ====================================================================
        
        /// <summary>
        /// 에러 발생 시 추가 알림 (onError 콜백과 별도)
        /// </summary>
        public event Action<string> OnError;
        
        // ====================================================================
        // Properties
        // ====================================================================
        
        /// <summary>
        /// 설정된 patch labels (읽기 전용)
        /// </summary>
        public IReadOnlyList<string> PatchLabels { get; }
        
        /// <summary>
        /// 마지막 PatchProc 결과 캐시
        /// </summary>
        public PatchInfo LastPatchInfo { get; }
        
        // ====================================================================
        // Methods
        // ====================================================================
        
        /// <summary>
        /// 라벨별 다운로드 크기 계산
        /// </summary>
        /// <param name="onDone">성공 시 PatchInfo 전달</param>
        /// <param name="onError">실패 시 에러 메시지 전달 (반드시 호출)</param>
        /// <param name="overrideLabels">patchLabels 대신 사용할 라벨 (optional)</param>
        public IEnumerator PatchProc(
            Action<PatchInfo> onDone,
            Action<string> onError = null,
            IReadOnlyList<string> overrideLabels = null);
        
        /// <summary>
        /// 라벨별 의존 번들 다운로드
        /// </summary>
        /// <param name="onProgress">진행률 0~1</param>
        /// <param name="onSuccess">성공 완료</param>
        /// <param name="onError">실패 시 에러 메시지 전달 (반드시 호출, onSuccess는 호출 안함)</param>
        /// <param name="overrideLabels">patchLabels 대신 사용할 라벨 (optional)</param>
        public IEnumerator DownloadProc(
            Action<float> onProgress,
            Action onSuccess,
            Action<string> onError = null,
            IReadOnlyList<string> overrideLabels = null);
    }
}
```

---

## Hard Rules (정책)

### 1. ResSingleton 계약

**`Load(resourcePath)` 선행 호출 필수**

```csharp
// 부팅 시
DownloadManager.Load("Devian/DownloadManager");

// 이후 사용
var dm = DownloadManager.Instance;
StartCoroutine(dm.PatchProc(info => { ... }));
```

- 프로젝트에 `Assets/Resources/Devian/DownloadManager.prefab` 필요
- 프리팹에 `DownloadManager` 컴포넌트 부착
- 인스펙터에서 `patchLabels` 설정

### 2. 라벨 정규화

**PatchProc/DownloadProc 실행 전 라벨 정규화**

- `Trim()`
- Empty 제거
- `Distinct()` (Ordinal)
- `Sort()` (Ordinal)

**정규화된 리스트가 비어있으면:**
- PatchProc: `TotalSize = 0` 결과 즉시 반환
- DownloadProc: 즉시 `onProgress(1)` + `onSuccess()` 호출

### 3. 실패 시 콜백 호출 필수 (조용히 종료 금지)

**실패 시 반드시 `onError` 콜백 호출**

```csharp
// CORRECT: 실패 시 onError 호출
if (sizeOp.Status == AsyncOperationStatus.Failed)
{
    var msg = "...";
    onError?.Invoke(msg);
    yield break;
}

// WRONG: 조용히 종료 (금지)
if (sizeOp.Status == AsyncOperationStatus.Failed)
{
    yield break; // FAIL - 호출자가 실패 판정 불가
}
```

- `OnError` 이벤트도 함께 발생
- 실패 후 `onSuccess`는 호출 금지

### 4. forceClearDependencyCache 기본 false

**운영 환경에서 캐시 삭제는 위험**

- 기본값: `false`
- `true`일 때만 `Addressables.ClearDependencyCacheAsync(label)` 호출
- 테스트/개발 목적으로만 사용

### 5. Resources 직접 호출 금지

**DownloadManager 내부에서 `Resources.` 직접 호출 금지**

- 리소스 로딩은 `ResSingleton<T>.Load(path)`를 통해서만 발생
- 상속받은 ResSingleton이 Resources.Load 수행 (허용)

### 6. AssetManager 연동 규칙

**DownloadManager는 다운로드만 담당하고, 실제 로딩은 AssetManager가 수행한다.**

- **역할 분리**:
  - `DownloadManager`: 다운로드 크기 확인 + 번들 다운로드 (캐시에 저장)
  - `AssetManager`: 다운로드된 에셋을 로드하여 사용

- **연동 흐름**:
  1. `DownloadManager.PatchProc()` → 다운로드 필요 크기 확인
  2. `DownloadManager.DownloadProc()` → 번들 다운로드 (Addressables 캐시에 저장)
  3. `AssetManager.LoadBundleAssets(label)` → 다운로드된 에셋을 로드하여 사용

- **label/key 일치 권장**: 패치 대상 label과 AssetManager에서 사용하는 key는 동일 문자열로 운영하는 것을 권장

```csharp
// 다운로드 (DownloadManager)
yield return dm.DownloadProc(..., labels: new[] { "prefabs", "table-ndjson" });

// 로딩 (AssetManager) - 동일한 label/key 사용
yield return AssetManager.LoadBundleAssets<GameObject>("prefabs");
yield return AssetManager.LoadBundleAssets<TextAsset>("table-ndjson");
```

---

## Known Behavior (운영/디버깅용)

### TotalSize = 0 은 정상일 수 있다

**PatchProc에서 `TotalSize = 0`이 반환되는 경우:**

1. **이미 캐시에 존재**: 이전에 다운로드한 번들이 캐시에 남아있음
2. **Editor Play Mode**: AssetDatabase 기반으로 동작하여 다운로드 개념이 없음
3. **Local-only 그룹**: Addressables 그룹이 로컬 빌드로 설정되어 있음
4. **빈 라벨 리스트**: 정규화 후 라벨이 없음

### 로딩 실패 원인 분석

**다운로드 성공 후 로딩이 실패하는 경우:**

- DownloadManager 문제가 **아닐** 가능성이 높음
- 확인 사항:
  - Addressables 카탈로그가 최신인지
  - 에셋 키/label이 정확한지
  - AssetManager.LoadBundleAssets 호출 시 타입이 맞는지
  - Addressables 그룹 설정이 올바른지

---

## 사용 예시

### 부팅 시퀀스

```csharp
using Devian;
using UnityEngine;

public class BootSequence : MonoBehaviour
{
    IEnumerator Start()
    {
        // 1. DownloadManager 로드
        DownloadManager.Load("Devian/DownloadManager");
        var dm = DownloadManager.Instance;
        
        // 2. 패치 크기 확인
        PatchInfo patchInfo = null;
        yield return dm.PatchProc(
            info => patchInfo = info,
            err => Debug.LogError($"Patch failed: {err}")
        );
        
        if (patchInfo == null)
        {
            // 에러 발생
            yield break;
        }
        
        Debug.Log($"Total download: {patchInfo.TotalSize} bytes");
        
        // 3. 다운로드 실행
        if (patchInfo.TotalSize > 0)
        {
            bool success = false;
            yield return dm.DownloadProc(
                progress => Debug.Log($"Progress: {progress * 100:F1}%"),
                () => success = true,
                err => Debug.LogError($"Download failed: {err}")
            );
            
            if (!success)
            {
                yield break;
            }
        }
        
        Debug.Log("Download complete!");
    }
}
```

### 특정 라벨만 다운로드

```csharp
// overrideLabels로 특정 라벨만 처리
var customLabels = new List<string> { "table-ndjson", "prefabs" };

yield return dm.PatchProc(
    info => Debug.Log($"Custom download size: {info.TotalSize}"),
    err => Debug.LogError(err),
    customLabels
);
```

---

## 프리팹 요구사항 (문서 안내만, 레포 강제 금지)

프로젝트에서 아래 프리팹을 생성해야 한다:

1. **프리팹 생성**: `Assets/Resources/Devian/DownloadManager.prefab`
2. **컴포넌트 부착**: `DownloadManager` 스크립트 추가
3. **인스펙터 설정**: `patchLabels`에 다운로드할 Addressables Label 입력

> **Note**: 이 프리팹 생성은 Unity Editor 작업이므로 레포에 YAML로 강제 추가하지 않는다.

---

## DoD (검증 가능)

### PASS 조건

- [ ] `DownloadManager.cs` (UPM + UnityExample) 최상단 SSOT가 이 문서를 가리킴
- [ ] `DownloadManager`가 `ResSingleton<DownloadManager>` 상속
- [ ] `patchLabels: List<string>` 인스펙터 필드 존재
- [ ] `forceClearDependencyCache: bool` 기본값 `false`
- [ ] `PatchProc`/`DownloadProc`가 라벨 정규화 적용
- [ ] 실패 시 `onError` 반드시 호출 (조용히 종료 0건)
- [ ] `Resources.` 직접 호출 0건 (ResSingleton.Load 사용은 예외)

### FAIL 조건

- `DownloadManager`가 ResSingleton을 상속하지 않음
- 실패 시 `onError` 호출 없이 `yield break`만 수행
- `forceClearDependencyCache` 기본값이 `true`
- `Resources.` 직접 호출 존재 (ResSingleton 제외)
- SSOT 주석이 다른 문서를 가리킴

---

## Reference

- Related: `skills/devian-core/03-ssot/SKILL.md` (Foundation Package SSOT)
- Related: `skills/devian-unity/30-unity-components/01-singleton/SKILL.md` (ResSingleton)
- Related: `skills/devian-unity/30-unity-components/10-asset-manager/SKILL.md` (AssetManager)
- Related: `skills/devian-data/33-string-table/SKILL.md` (String Table 규약)

---

## String Table Integration (Hard Rules)

### Label = Key 규약

**String Table 에셋은 Address(Key)와 Label이 동일해야 한다.**

```
Address(Key): string/{format}/{Language}/{TableName}
Label:        string/{format}/{Language}/{TableName}
```

예시:
```
string/ndjson/Korean/UIText
string/pb64/English/ItemName
```

### overrideLabels 사용 규칙

**String Table 다운로드는 반드시 `overrideLabels` 파라미터를 사용한다.**

```csharp
// String Table 다운로드 예시
var labels = new[] { "string/ndjson/Korean/UIText" };

yield return dm.PatchProc(
    info => { },
    err => Debug.LogError(err),
    overrideLabels: labels
);

yield return dm.DownloadProc(
    p => { },
    () => { },
    err => Debug.LogError(err),
    overrideLabels: labels
);
```

### Language 미지정 시 기본값

**language가 `Unknown`이거나 미지정이면 `English`로 치환한 뒤 label을 구성한다.**

```csharp
var lang = language == SystemLanguage.Unknown ? SystemLanguage.English : language;
var label = $"string/{format}/{lang}/{tableName}";
```
