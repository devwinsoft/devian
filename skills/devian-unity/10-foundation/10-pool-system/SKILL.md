# 10-pool-system

Status: ACTIVE  
AppliesTo: v17  
Type: Component Specification

## 1. 목적

Unity에서 프리팹 기반 객체 재사용을 위한 PoolManager 템플릿 제공.
PoolManager는 **AutoSingleton 기반 Registry**이며, 사용자는 **IPoolFactory 확장 메서드로 Spawn/Despawn**한다.
`PoolManager`는 scene/prefab에 미리 붙이지 않고, script code의 `Instance` 접근으로 생성된다.

---

## 2. 범위

### 포함

- `IPoolable` 인터페이스
- `IPoolFactory` 인터페이스
- `PoolManager` AutoSingleton 클래스 (Registry 역할)
- `Pool<T>` 제네릭 풀 및 비제네릭 `IPool` 인터페이스
- `PoolTag` MonoBehaviour (인스턴스→풀 결정적 매핑)
- `PoolFactoryExtensions` 확장 메서드 (factory.Spawn/Despawn)
- **Type/PoolName/Inactive 디버깅 하이어라키**
- 메인 스레드 강제 (비메인 throw)

### 제외

- 비동기 큐잉/스레드 마샬링 (throw로 단순화)

---

## 3. 네임스페이스

모든 생성 코드는 `namespace Devian` 고정.

---

## 4. 사용자 API (핵심)

**사용자는 PoolManager로 직접 Spawn하지 않는다.** IPoolFactory 확장 메서드를 사용한다:

```csharp
// Spawn
var enemy = myFactory.Spawn<Enemy>("Goblin", position, rotation);

// Despawn (PoolTag 기반 라우팅)
myFactory.Despawn(enemy);
// 또는
PoolManager.Instance.Despawn(enemy);
```

---

## 5. 디버깅 하이어라키 구조 (핵심)

Unity Hierarchy에서 풀 오브젝트가 Type → PoolName → Inactive로 정렬된다:

```
[PoolManager]
  <TypeName>                    # typeof(T).Name
    <PoolName>                  # Spawn(name)의 name (프리팹 이름)
      Inactive                  # Despawn된 비활성 오브젝트
```

- **Active 폴더는 생성하지 않는다.**
- Spawn(parent=null) 시 오브젝트는 parent 없이 스폰된다.
- Despawn 시 오브젝트는 항상 `Inactive` 아래로 이동한다.

### 예시

```
[PoolManager]
  Enemy
    Goblin
      Inactive
    Orc
      Inactive
  Projectile
    Fireball
      Inactive
```

### PoolName 결정 규칙

1. `Spawn(name)`의 `name` 파라미터를 사용
2. 정규화:
   - null/empty/whitespace → `"Default"`
   - 64자 초과 → 앞부분 64자만 사용
   - 슬래시(`/`, `\`) → `_`로 치환

---

## 6. 키/풀 규약

- **Pool identity** = `PoolId (int)` — 내부 등록 시 발급
- **Pool lookup key** = `(factory reference, component type, poolName)` — 3개 요소로 풀 구분
- **Spawn key** = `string name` (prefab의 `gameObject.name`)
- 각 `(factory, type, poolName)` 조합마다 별도의 Pool<T> 인스턴스가 생성됨

---

## 7. Parent 정책 (Spawn)

Spawn 시 `parent` 인자에 따라 오브젝트 부모가 결정된다:

| parent 인자 | 결과 |
|-------------|------|
| `null` | parent 없이 스폰됨 (`PoolOptions.Root` 지정 시 해당 루트 사용) |
| `Transform` 제공 | 제공된 parent 아래로 이동 (게임 로직 우선) |

### Despawn

Despawn 시에는 **항상** `[PoolManager]/{Type}/{PoolName}/Inactive` 아래로 이동.
비활성 풀링 오브젝트는 한 곳에 모여야 디버깅이 용이함.

---

## 8. PoolTag (인스턴스→풀 결정적 매핑)

```csharp
namespace Devian
{
    [DisallowMultipleComponent]
    public sealed class PoolTag : MonoBehaviour
    {
        public int PoolId { get; private set; }
        public string PoolName { get; private set; }
        public bool IsSpawned { get; private set; }

        internal void SetPoolInfo(int poolId, string poolName);
        internal void MarkSpawned();
        internal void MarkDespawned();
    }
}
```

### 규약

- Spawn 시 PoolManager가 `PoolTag`를 인스턴스에 부착/갱신하여 `PoolId`, `PoolName`을 기록하고, `IsSpawned = true`로 마킹
- Despawn 시 PoolManager가 `PoolTag.PoolId`로 풀을 찾아 반환, `IsSpawned = false`로 마킹
- **Tag 없으면 Despawn 거부** (throw) — 휴리스틱 추측 금지
- **IsSpawned로 이중 Despawn 방지** — 이미 `IsSpawned = false`면 Despawn 무시

---

## 9. IPoolable (콜백 이름 고정)

```csharp
public interface IPoolable
{
    void OnPoolSpawned();
    void OnPoolDespawned();
}
```

> **금지**: `OnSpawned` / `OnDespawned` 명칭 사용 금지. 반드시 `OnPoolSpawned` / `OnPoolDespawned` 사용.

---

## 10. IPoolFactory (확장성)

```csharp
public interface IPoolFactory
{
    UnityEngine.GameObject GetPrefab(string name);
    System.Type GetPoolType(UnityEngine.GameObject prefab);
    UnityEngine.Component CreateInstance(UnityEngine.GameObject prefab);
    void DestroyInstance(UnityEngine.Component instance);
}
```

### 규약

- `GetPoolType`: prefab에서 `IPoolable` 구현 컴포넌트를 찾아 해당 컴포넌트 타입을 반환해야 함.
- **IPoolFactory 인터페이스 시그니처 변경 금지** — 기존 Factory 구현 호환성 유지

### DestroyInstance 정본 (Hard Rule)

`DestroyInstance`는 Unity Object Destroy 규약을 따른다 (Editor 모드: `DestroyImmediate`, Runtime: `Destroy`, 항상 `instance.gameObject` 대상).

### 10.1 InspectorPoolFactory 규약

형태: `public sealed class InspectorPoolFactory : MonoBehaviour, IPoolFactory`

#### 프리팹 등록 방식

```csharp
[SerializeField] private GameObject[] _prefabs;
```

#### 내부 캐시

```csharp
private Dictionary<string, GameObject> _prefabByName;
```

- `Awake()` 또는 `OnEnable()`에서 캐시 구성
- `OnValidate()`에서 에디터 편의를 위해 재구성

#### GetPrefab(name)

```csharp
_prefabByName.TryGetValue(name, out var prefab) ? prefab : null
```

#### GetPoolType(prefab)

- `prefab.GetComponents<Component>()`를 순회
- 각 컴포넌트 타입이 `IPoolable`을 구현하는지 `IsAssignableFrom`으로 확인
- 찾으면 해당 컴포넌트 타입 반환
- 못 찾으면 `InvalidOperationException`

#### CreateInstance(prefab)

```csharp
var go = UnityEngine.Object.Instantiate(prefab);
var poolType = GetPoolType(prefab);
return go.GetComponent(poolType);
```

#### DestroyInstance(instance)

Unity Object Destroy 규약 준수 (Editor: `DestroyImmediate`, Runtime: `Destroy`, 항상 `instance.gameObject`):

```csharp
#if UNITY_EDITOR
if (!UnityEngine.Application.isPlaying)
{
    UnityEngine.Object.DestroyImmediate(instance.gameObject);
    return;
}
#endif
UnityEngine.Object.Destroy(instance.gameObject);
```

- **컴포넌트가 아닌 `instance.gameObject`를 Destroy**

### 10.2 BundlePoolFactory 규약

형태: `public sealed class BundlePoolFactory : SimpleSingleton<BundlePoolFactory>, IPoolFactory`

#### 생성자

```csharp
public BundlePoolFactory() { }  // SimpleSingleton의 new() 제약 충족
```

#### Generic GetPrefab (권장)

```csharp
public TAsset GetPrefab<TAsset>(string name) where TAsset : UnityEngine.Object
{
    UnityMainThread.EnsureOrThrow("BundlePoolFactory.GetPrefab<TAsset>");
    return AssetManager.GetAsset<TAsset>(name);
}
```

- `AssetManager.GetAsset<T>(name)` 그대로 사용
- Generic으로 다양한 에셋 타입 지원

#### IPoolFactory.GetPrefab (명시적 구현)

```csharp
GameObject IPoolFactory.GetPrefab(string name)
{
    return GetPrefab<GameObject>(name);
}
```

- IPoolFactory 인터페이스는 GameObject 고정
- Generic API를 GameObject로 연결

#### GetPoolType/CreateInstance/DestroyInstance

`InspectorPoolFactory`와 동일 규약.
모든 public API에 `UnityMainThread.EnsureOrThrow(...)` 적용.

### 10.3 BundlePool 규약 (권장 사용법)

형태: `public static class BundlePool`

**사용자는 BundlePool을 통해 풀링하는 것을 권장**

#### Spawn

```csharp
public static T Spawn<T>(
    string name,
    Vector3 position = default,
    Quaternion rotation = default,
    Transform parent = null,
    PoolOptions options = default)
    where T : Component, IPoolable
{
    UnityMainThread.EnsureOrThrow("BundlePool.Spawn");
    return BundlePoolFactory.Instance.Spawn<T>(name, position, rotation, parent, options);
}
```

#### Despawn

```csharp
public static void Despawn(Component instance)
{
    UnityMainThread.EnsureOrThrow("BundlePool.Despawn");
    BundlePoolFactory.Instance.Despawn(instance);
}
```

#### ClearAll

```csharp
public static void ClearAll()
{
    UnityMainThread.EnsureOrThrow("BundlePool.ClearAll");
    PoolManager.Instance.ClearAll();
}
```

### 10.4 Factory 사용 예시

#### BundlePool (권장)

```csharp
// Spawn
var enemy = BundlePool.Spawn<Enemy>("Goblin", position, rotation);

// Despawn
BundlePool.Despawn(enemy);
```

#### BundlePoolFactory (직접 사용)

```csharp
// Spawn (factory 확장 메서드)
var enemy = BundlePoolFactory.Instance.Spawn<Enemy>("Goblin", position, rotation);

// Despawn
BundlePoolFactory.Instance.Despawn(enemy);
```

### 10.5 Factory 주의/금지 사항

1. **에셋 사전 로딩 필수**: BundlePoolFactory는 AssetManager를 통해 에셋을 가져옴. 에셋이 캐시에 없으면 실패함.
2. **name은 에셋 키이자 풀 키**: 오타/대소문자 차이가 있으면 다른 풀로 분리되어 메모리 낭비 발생.
3. **`AssetManager.Instance` 사용 금지** (static 메서드 사용)
4. **`Resources` / `Addressables` 사용 금지**

---

## 11. PoolManager API (AutoSingleton)

```csharp
public sealed class PoolManager : AutoSingleton<PoolManager>
{
    // === Public API ===
    public static PoolManager Instance { get; }  // AutoSingleton 제공
    
    public void Despawn(Component instance);     // PoolTag 기반 라우팅
    public void Clear(int poolId);
    public void ClearAll();
    
    // === Internal (factory.Spawn에서 사용) ===
    internal static string NormalizePoolName(string name);
    internal Transform _GetTypeRoot(Type componentType);
    internal NameRoots _GetNameRoots(Type componentType, string poolName);
    internal IPool _GetOrCreatePool<T>(IPoolFactory factory, string poolName, PoolOptions options)
        where T : Component, IPoolable;
    internal void _TrackSpawned(IPool pool, Component instance, string poolName);
}
```

### Despawn

- 메인스레드 체크
- null 체크
- **PoolTag 없으면 throw** (어느 풀에서 왔는지 모르면 반환 불가)
- `tag.PoolId`로 풀 찾아서 `pool.Despawn(instance)` 호출

### _GetNameRoots (internal)

- Type/PoolName별 Root/Inactive 루트 생성 및 캐시
- `NameRoots` struct로 `(Root, Inactive)` 반환

---

## 12. PoolFactoryExtensions (사용자 진입점)

```csharp
public static class PoolFactoryExtensions
{
    public static T Spawn<T>(
        this IPoolFactory factory,
        string name,
        Vector3 position = default,
        Quaternion rotation = default,
        Transform parent = null,
        PoolOptions options = default) where T : Component, IPoolable;

    public static void Despawn(this IPoolFactory factory, Component instance);
}
```

### Spawn<T>

- 메인스레드 체크
- `name`을 poolName으로 사용
- `PoolManager.Instance._GetOrCreatePool<T>(factory, name, options)` 호출
- `pool.Spawn(name, position, rotation, parent)` 반환

### Despawn

- 메인스레드 체크
- `PoolManager.Instance.Despawn(instance)` 위임
- factory 인자는 API 일관성용 (라우팅은 PoolTag 기반)

---

## 13. PoolOptions

```csharp
public struct PoolOptions
{
    public int MaxSize;           // 최대 비활성 인스턴스 수 (기본 512)
    public Transform Root;        // Spawn 시 parent=null일 때 사용할 선택적 루트 (caller가 지정)
    public Transform InactiveRoot;// Despawn 시 사용 (PoolManager가 설정)
    public int Prewarm;           // 프리웜 수량 (기본 0)
}
```

> **Note**: `InactiveRoot`는 PoolManager가 자동으로 설정한다. `Root`는 `parent=null` 기본 동작을 덮어쓰고 싶을 때만 caller가 직접 지정한다.

---

## 14. 스레드 규약

- 모든 API는 **메인 스레드에서만 허용**
- 비메인 호출 시 `InvalidOperationException` throw
- `UnityMainThread.EnsureOrThrow(string context)` 형태로 통일

---

## 15. Clear 규약

- `Clear`는 **inactive만 Destroy** 후 큐 비움
- active 강제 회수는 제외

---

## 16. 풀 안전 규칙 (Hard Rule)

### 16.1 Despawn 순서 (정본)

```
1. OnPoolDespawned() 호출 (사용자 코드)
2. SetActive(false)
3. SetParent(InactiveRoot)
4. inactive 큐에 추가
```

- **OnPoolDespawned()가 먼저 호출되어야 함** (사용자가 비활성화 전 정리 가능)
- 순서를 바꾸는 것은 금지

### 16.2 Destroy 예외 처리 (정본)

**OnPoolDespawned() 중 Destroy:**

```csharp
instance.OnPoolDespawned();

if (instance == null || instance.gameObject == null)
{
    throw new InvalidOperationException(
        "Pooled object was destroyed during OnPoolDespawned(). " +
        "Destroying pooled objects is forbidden. Use Despawn only.");
}
```

- 사용자가 콜백에서 Destroy하면 즉시 예외 발생
- "조용히 무시"는 금지

**inactive 큐의 Destroy된 엔트리:**

```csharp
T instance = null;
while (_inactiveQueue.Count > 0 && instance == null)
{
    instance = _inactiveQueue.Dequeue();
}
```

- Spawn 시 null(Unity null 포함) 엔트리는 자동 제거
- Destroy된 오브젝트가 반환되지 않음

**PoolTag.IsSpawned 상태 플래그:**

- Active 저장(HashSet)은 사용하지 않고, PoolTag.IsSpawned로 이중 Despawn만 방지
- Spawn 시 `tag.MarkSpawned()` 호출
- Despawn 시 `tag.IsSpawned == false`면 return (중복 Despawn 무시)
- Despawn 처리 시작 시 `tag.MarkDespawned()` 호출

### 16.3 PoolTag 불변 규칙 (정본)

```csharp
internal void SetPoolInfo(int poolId, string poolName)
{
    if (PoolId != 0 && PoolId != poolId)
    {
        throw new InvalidOperationException("PoolTag.PoolId must never change.");
    }
    if (!string.IsNullOrEmpty(PoolName) && PoolName != poolName)
    {
        throw new InvalidOperationException("PoolTag.PoolName must never change.");
    }
    PoolId = poolId;
    PoolName = poolName;
}
```

- **최초 세팅 후 변경 금지**
- 같은 값 재세팅은 허용 (idempotent)
- 다른 풀로 "재귀속" 시도 시 즉시 예외

---

## 17. Generated Output (정본)

### 생성 대상 패키지

- `com.devian.foundation`

### 생성 위치 (고정)

```
com.devian.foundation/Runtime/Unity/
├── _Shared/
│   └── UnityMainThread.cs     (공용 내부 헬퍼)
└── Pool/
    ├── IPoolable.cs
    ├── PoolOptions.cs
    ├── IPool.cs
    ├── Pool.cs
    ├── PoolManager.cs
    ├── PoolTag.cs
    └── Factory/
        ├── IPoolFactory.cs
        ├── PoolFactoryExtensions.cs
        ├── BundlePool.cs
        ├── BundlePoolFactory.cs
        └── InspectorPoolFactory.cs
```

### 수기 코드 정책 (Static UPM)

- `com.devian.foundation`의 `Runtime/Unity/Pool/` 폴더는 **고정 유틸 수기 코드**
- 생성기는 `Generated/` 폴더만 처리하며, `Pool/` 폴더를 clean/generate하지 않음
- Static UPM은 소스 복사 기반 (`framework-cs/upm/` → `UnityExample/Packages/`)

### 파일 목록 (11개)

| 파일 | 타입 | 네임스페이스 |
|------|------|-------------|
| `IPoolable.cs` | `IPoolable` | `Devian` |
| `PoolOptions.cs` | `PoolOptions` (struct) | `Devian` |
| `IPool.cs` | `IPool` | `Devian` |
| `Pool.cs` | `Pool<T>` | `Devian` |
| `PoolManager.cs` | `PoolManager` (AutoSingleton) | `Devian` |
| `PoolTag.cs` | `PoolTag` (MonoBehaviour) | `Devian` |
| `Factory/IPoolFactory.cs` | `IPoolFactory` | `Devian` |
| `Factory/PoolFactoryExtensions.cs` | `PoolFactoryExtensions` (static) | `Devian` |
| `Factory/BundlePool.cs` | `BundlePool` (static facade) | `Devian` |
| `Factory/BundlePoolFactory.cs` | `BundlePoolFactory` (SimpleSingleton) | `Devian` |
| `Factory/InspectorPoolFactory.cs` | `InspectorPoolFactory` (MonoBehaviour) | `Devian` |

---

## DoD (완료 정의)

### Pool Core
- [x] `Runtime/Unity/Pool/`에 6개 파일 존재 (IPoolable, PoolOptions, IPool, Pool, PoolManager, PoolTag)
- [x] PoolManager가 AutoSingleton 상속
- [x] PoolTag 불변 규칙 및 이중 Despawn 방지
- [x] 디버깅 하이어라키 Type/PoolName/Inactive 구조
- [x] 모든 public API에 메인 스레드 강제

### Pool Factories
- [x] `Runtime/Unity/Pool/Factory/`에 5개 파일 존재 (IPoolFactory, PoolFactoryExtensions, InspectorPoolFactory, BundlePoolFactory, BundlePool)
- [x] `BundlePoolFactory`가 `SimpleSingleton<BundlePoolFactory>` 상속
- [x] `BundlePoolFactory.GetPrefab<TAsset>(name)` Generic API 제공
- [x] `BundlePool`이 static facade로 존재하며 사용자 권장 API로 명시
- [x] `DestroyInstance`는 Unity Object Destroy 규약 준수 (Editor: DestroyImmediate, Runtime: Destroy, gameObject 대상)
- [x] 모든 public API에 메인 스레드 강제

---

## Reference

- Parent: `skills/devian-unity/10-foundation/SKILL.md`
- Related: `15-singleton/SKILL.md` (Singleton v3)
- Related: `18-asset-manager/SKILL.md` (AssetManager API)
