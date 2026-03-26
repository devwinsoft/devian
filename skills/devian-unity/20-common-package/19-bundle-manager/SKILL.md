# 19-bundle-manager

Status: ACTIVE  
AppliesTo: v13

## SSOT

이 문서는 `BundleManager` 컴포넌트의 **API, 정책, 검증 규칙**을 정의한다.

---

## 목적/범위

**Addressables Label 기반 패치/다운로드를 제공하는 Unity 전용 Generic CompoSingleton.**

- **Generic CompoSingleton**: `BundleManager<T> : CompoSingleton<T>` — concrete 서브클래스를 Bootstrap prefab에 배치하여 사용
- **InitializeAsync**: 라벨별 다운로드 필요 용량 계산 → `async Task<CommonResult<PatchInfo>>`
- **DownloadAsync**: 라벨별 의존 번들 다운로드 (가중치 기반 진행률) → `async Task<CommonResult>`
- **실패 처리**: `CommonResult.Failure` 반환 + `OnError` 이벤트 ("조용히 종료" 금지)

---

## 소스 경로

| 위치 | 경로 |
|------|------|
| UPM 소스 | `framework-cs/upm/com.devian.foundation/Samples~/CommonPackage/Runtime/Unity/AssetManager/BundleManager.cs` |
| UPM 소스 | `framework-cs/upm/com.devian.foundation/Samples~/CommonPackage/Runtime/Unity/Settings/BundleSettings.cs` |
| UnityExample | `framework-cs/apps/UnityExample/Packages/com.devian.foundation/Samples~/CommonPackage/Runtime/Unity/AssetManager/BundleManager.cs` (derived output) |
| UnityExample | `framework-cs/apps/UnityExample/Packages/com.devian.foundation/Samples~/CommonPackage/Runtime/Unity/Settings/BundleSettings.cs` (derived output) |

---

## Prerequisites (필수 의존성)

**BundleManager는 Unity Addressables 기반이므로 다음 의존성이 필수:**

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

## BundleSettings 연동

설정은 `BundleSettings` ScriptableObject에서 로드한다.

- 클래스: `BundleSettings : ScriptableObject` (`Runtime/Unity/Settings/BundleSettings.cs`)
- Resources 경로: `Devian/BundleSettings`
- 프로젝트 에셋 경로: `Assets/Resources/Devian/BundleSettings.asset`
- 접근 방식: `ensureSettings()?.ForceClearDependencyCache`

| 필드 | 타입 | 기본값 | 설명 |
|------|------|--------|------|
| `ForceClearDependencyCache` | `bool` | `false` | Size 계산 전 캐시 삭제 (운영 위험, 테스트용) |

> **주의**: `ForceClearDependencyCache`를 `true`로 설정하면 매번 캐시를 삭제하므로 운영 환경에서 사용 금지.

BundleManager는 `ensureSettings()`로 최초 접근 시 `BundleSettings`를 로드하며, `_settingsLoaded` 플래그로 중복 로드를 방지한다. 설정 에셋이 없으면 해당 기능이 비활성(기본값) 상태로 동작한다.

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

### BundleManager\<T\>

```csharp
namespace Devian
{
    public abstract class BundleManager<T> : CompoSingleton<T> where T : BundleManager<T>
    {
        // ====================================================================
        // Settings
        // ====================================================================

        private BundleSettings? _settings;
        private bool _settingsLoaded;

        private BundleSettings? ensureSettings();

        // ====================================================================
        // Events
        // ====================================================================

        /// <summary>
        /// 에러 발생 시 추가 알림 (CommonResult.Failure 외 추가 채널)
        /// </summary>
        public event Action<string> OnError;

        // ====================================================================
        // Properties
        // ====================================================================

        /// <summary>
        /// 마지막 InitializeAsync 결과 캐시
        /// </summary>
        public PatchInfo LastPatchInfo { get; }

        // ====================================================================
        // PatchLabels
        // ====================================================================

        /// <summary>
        /// 패치/다운로드 대상 라벨 목록. concrete 서브클래스가 정의한다.
        /// </summary>
        protected abstract IReadOnlyList<string> PatchLabels { get; }

        // ====================================================================
        // Methods
        // ====================================================================

        /// <summary>
        /// PatchLabels 기준으로 다운로드 필요 용량을 계산한다.
        /// </summary>
        public async Task<CommonResult<PatchInfo>> InitializeAsync();

        /// <summary>
        /// PatchLabels 기준으로 의존 번들을 다운로드한다.
        /// </summary>
        /// <param name="maxProgress">진행률 상한값 (0~maxProgress 범위로 보고)</param>
        /// <param name="onProgress">진행률 콜백 (0~maxProgress)</param>
        public async Task<CommonResult> DownloadAsync(float maxProgress, Action<float> onProgress = null);

        /// <summary>
        /// 번들 에셋을 로드한다. 서브클래스가 override하여 구현한다.
        /// </summary>
        /// <param name="language">로드할 언어</param>
        /// <param name="startProgress">시작 진행률 값</param>
        /// <param name="onProgress">진행률 콜백 (startProgress~1.0)</param>
        public virtual Task LoadBundlesAsync(SystemLanguage language, float startProgress, Action<float> onProgress = null);
    }
}
```

---

## Hard Rules (정책)

### 1. Generic CompoSingleton 배치 규칙

**BundleManager\<T\>는 `abstract class : CompoSingleton<T>`이다. 프로젝트별 concrete 서브클래스를 Bootstrap prefab에 부착한다.**

```csharp
// concrete 서브클래스 정의
public class MyBundleManager : BundleManager<MyBundleManager> { }

// 접근 — concrete 타입으로 조회 (Registry key = MyBundleManager)
var result = await MyBundleManager.Instance.InitializeAsync(labels);
```

- Bootstrap prefab에 concrete 서브클래스를 부착한다
- `ConcreteType.Instance`로 접근한다 (CompoSingleton이 제공, Registry key = concrete 타입)
- runtime `AddComponent`로 생성하지 않는다

### 2. 빈 라벨 처리

**라벨 리스트가 비어있으면:**
- InitializeAsync: `TotalSize = 0` 인 `CommonResult<PatchInfo>.Success` 즉시 반환
- DownloadAsync: 즉시 `onProgress(maxProgress)` + `CommonResult.Ok()` 반환

### 3. 실패 시 CommonResult.Failure 반환 필수 (조용히 종료 금지)

**실패 시 반드시 `CommonResult.Failure` 반환 + `OnError` 이벤트 발생**

```csharp
// CORRECT: 실패 시 Failure 반환
if (sizeOp.Status == AsyncOperationStatus.Failed)
{
    var msg = "...";
    Debug.LogError(msg);
    RaiseError(msg);
    return CommonResult<PatchInfo>.Failure(COMMON_ERROR_TYPE.COMMON_UNKNOWN, msg);
}

// WRONG: 조용히 성공 반환 (금지)
if (sizeOp.Status == AsyncOperationStatus.Failed)
{
    return CommonResult<PatchInfo>.Success(...); // FAIL - 호출자가 실패 판정 불가
}
```

- `OnError` 이벤트도 함께 발생
- 실패 시 `CommonResult.Ok()` 반환 금지

### 4. forceClearDependencyCache 기본 false

**운영 환경에서 캐시 삭제는 위험**

- 기본값: `false` (BundleSettings ScriptableObject)
- `ensureSettings()?.ForceClearDependencyCache == true` 일 때만 `Addressables.ClearDependencyCacheAsync(label)` 호출
- 테스트/개발 목적으로만 사용

### 5. Resources 직접 호출 제한

**BundleManager 내부에서 `Resources.` 직접 호출은 BundleSettings 로드에만 허용한다.**

- `Resources.Load<BundleSettings>(BundleSettings.ResourcesPath)` — 허용 (ensureSettings() 내부)
- BundleManager는 Resources 기반 singleton 로딩을 담당하지 않는다
- prefab/scene 배치와 bootstrap wiring은 소비자 레이어가 담당한다

### 6. AssetManager 연동 규칙

**BundleManager는 다운로드만 담당하고, 실제 로딩은 AssetManager가 수행한다.**

- **역할 분리**:
  - `BundleManager`: 다운로드 크기 확인 + 번들 다운로드 (캐시에 저장)
  - `AssetManager`: 다운로드된 에셋을 로드하여 사용

- **연동 흐름**:
  1. `await bundleManager.InitializeAsync(labels)` → 다운로드 필요 크기 확인
  2. `await bundleManager.DownloadAsync(labels)` → 번들 다운로드 (Addressables 캐시에 저장)
  3. `AssetManager.LoadBundleAssets(label)` → 다운로드된 에셋을 로드하여 사용

- **label/key 일치 권장**: 패치 대상 label과 AssetManager에서 사용하는 key는 동일 문자열로 운영하는 것을 권장

```csharp
// 다운로드 (BundleManager)
await dm.DownloadAsync(new[] { "prefabs", "table-ndjson" });

// 로딩 (AssetManager) - 동일한 label/key 사용
await AssetManager.LoadBundleAssets<GameObject>("prefabs");
await AssetManager.LoadBundleAssets<TextAsset>("table-ndjson");
```

---

## Known Behavior (운영/디버깅용)

### TotalSize = 0 은 정상일 수 있다

**InitializeAsync에서 `TotalSize = 0`이 반환되는 경우:**

1. **이미 캐시에 존재**: 이전에 다운로드한 번들이 캐시에 남아있음
2. **Editor Play Mode**: AssetDatabase 기반으로 동작하여 다운로드 개념이 없음
3. **Local-only 그룹**: Addressables 그룹이 로컬 빌드로 설정되어 있음
4. **빈 라벨 리스트**: 전달된 라벨이 없음

### 로딩 실패 원인 분석

**다운로드 성공 후 로딩이 실패하는 경우:**

- BundleManager 문제가 **아닐** 가능성이 높음
- 확인 사항:
  - Addressables 카탈로그가 최신인지
  - 에셋 키/label이 정확한지
  - AssetManager.LoadBundleAssets 호출 시 타입이 맞는지
  - Addressables 그룹 설정이 올바른지

---

## 배치 요구사항

`BundleManager<T>`는 abstract이므로 직접 부착할 수 없다. 프로젝트별 concrete 서브클래스를 Bootstrap prefab에 부착한다.

- `ConcreteType.Instance`로 접근 (CompoSingleton 제공, Registry key = concrete 타입)
- runtime `AddComponent`로 생성 금지 (CompoSingleton 규칙)

---

## DoD (검증 가능)

### PASS 조건

- [ ] `BundleManager.cs` (UPM + UnityExample) 최상단 SSOT가 이 문서를 가리킴
- [ ] `BundleManager<T>`가 `abstract class BundleManager<T> : CompoSingleton<T> where T : BundleManager<T>`
- [ ] Bootstrap prefab에 concrete 서브클래스가 부착되어 있음
- [ ] `BundleSettings.ForceClearDependencyCache` 기본값 `false`
- [ ] 실패 시 `CommonResult.Failure` 반환 + `OnError` 이벤트 (조용히 종료 0건)
- [ ] `Resources.` 직접 호출은 `ensureSettings()` 내부 BundleSettings 로드만 허용

### FAIL 조건

- `BundleManager<T>`가 `abstract class : CompoSingleton<T> where T : BundleManager<T>`가 아님
- Bootstrap prefab에 concrete 서브클래스가 없음
- 실패 시 `CommonResult.Failure` 반환 없이 `CommonResult.Ok()` 반환
- `BundleSettings.ForceClearDependencyCache` 기본값이 `true`
- `Resources.` 직접 호출이 `ensureSettings()` 외에 존재
- SSOT 주석이 다른 문서를 가리킴

---

## Reference

- Related: `skills/devian/10-module/03-ssot/SKILL.md` (Foundation Package SSOT)
- Related: `skills/devian-unity/20-common-package/29-singleton/SKILL.md` (Singleton)
- Related: `skills/devian-unity/20-common-package/13-asset-manager/SKILL.md` (AssetManager)

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

### labels 파라미터 사용 규칙

**String Table 다운로드는 `labels` 파라미터에 라벨을 전달한다.**

```csharp
// String Table 다운로드 예시
var labels = new[] { "string/ndjson/Korean/UIText" };

var patchResult = await dm.InitializeAsync(labels);
if (patchResult.IsFailure) { Debug.LogError(patchResult.Error); return; }

var downloadResult = await dm.DownloadAsync(labels);
if (downloadResult.IsFailure) { Debug.LogError(downloadResult.Error); return; }
```

### Language 미지정 시 기본값

**language가 `Unknown`이거나 미지정이면 `English`로 치환한 뒤 label을 구성한다.**

```csharp
var lang = language == SystemLanguage.Unknown ? SystemLanguage.English : language;
var label = $"string/{format}/{lang}/{tableName}";
```
