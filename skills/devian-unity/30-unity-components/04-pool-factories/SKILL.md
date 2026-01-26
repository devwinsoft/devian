# 04-pool-factories

Status: ACTIVE  
AppliesTo: v20  
Type: Component Specification

## 1. 목적

`IPoolFactory`의 실사용 구현 2종 및 static facade 1종 제공:
- `InspectorPoolFactory` - 인스펙터에서 프리팹 등록 (MonoBehaviour)
- `BundlePoolFactory` - AssetBundle 기반 로딩 (SimpleSingleton + IPoolFactory)
- `BundlePool` - BundlePoolFactory의 static facade (권장 사용법)

---

## 2. 네임스페이스

모든 코드는 `namespace Devian` 고정.

---

## 3. 생성 대상 패키지

- `com.devian.unity`

---

## 4. 파일 위치 (정본)

```
com.devian.unity/Runtime/PoolFactories/
├── InspectorPoolFactory.cs
├── BundlePoolFactory.cs
└── BundlePool.cs
```

> **Note**: 이 폴더는 수기 코드이며, 생성기는 clean+generate하지 않음.

---

## 5. InspectorPoolFactory 규약

형태: `public sealed class InspectorPoolFactory : MonoBehaviour, IPoolFactory`

### 프리팹 등록 방식

```csharp
[SerializeField] private GameObject[] _prefabs;
```

### 내부 캐시

```csharp
private Dictionary<string, GameObject> _prefabByName;
```

- `Awake()` 또는 `OnEnable()`에서 캐시 구성
- `OnValidate()`에서 에디터 편의를 위해 재구성

### GetPrefab(name)

```csharp
_prefabByName.TryGetValue(name, out var prefab) ? prefab : null
```

### GetPoolType(prefab)

- `prefab.GetComponents<Component>()`를 순회
- 각 컴포넌트 타입이 구현하는 인터페이스 중 `IPoolable<>` (generic type definition) 찾기
- 찾으면 해당 컴포넌트 타입 반환
- 못 찾으면 `InvalidOperationException`

### CreateInstance(prefab)

```csharp
var go = UnityEngine.Object.Instantiate(prefab);
var poolType = GetPoolType(prefab);
return go.GetComponent(poolType);
```

### DestroyInstance(instance)

`00-unity-object-destruction/SKILL.md` 규약 준수:

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

---

## 6. BundlePoolFactory 규약

형태: `public sealed class BundlePoolFactory : SimpleSingleton<BundlePoolFactory>, IPoolFactory`

### 생성자

```csharp
public BundlePoolFactory() { }  // SimpleSingleton의 new() 제약 충족
```

### Generic GetPrefab (권장)

```csharp
public TAsset GetPrefab<TAsset>(string name) where TAsset : UnityEngine.Object
{
    UnityMainThread.EnsureOrThrow("BundlePoolFactory.GetPrefab<TAsset>");
    return AssetManager.GetAsset<TAsset>(name);
}
```

- `AssetManager.GetAsset<T>(name)` 그대로 사용
- Generic으로 다양한 에셋 타입 지원

### IPoolFactory.GetPrefab (명시적 구현)

```csharp
GameObject IPoolFactory.GetPrefab(string name)
{
    return GetPrefab<GameObject>(name);
}
```

- IPoolFactory 인터페이스는 GameObject 고정
- Generic API를 GameObject로 연결

### GetPoolType/CreateInstance/DestroyInstance

`InspectorPoolFactory`와 동일 규약.
모든 public API에 `UnityMainThread.EnsureOrThrow(...)` 적용.

---

## 7. BundlePool 규약 (권장 사용법)

형태: `public static class BundlePool`

**사용자는 BundlePool을 통해 풀링하는 것을 권장**

### Spawn

```csharp
public static T Spawn<T>(
    string name,
    Vector3 position = default,
    Quaternion rotation = default,
    Transform parent = null,
    PoolOptions options = default)
    where T : Component, IPoolable<T>
{
    UnityMainThread.EnsureOrThrow("BundlePool.Spawn");
    return BundlePoolFactory.Instance.Spawn<T>(name, position, rotation, parent, options);
}
```

### Despawn

```csharp
public static void Despawn(Component instance)
{
    UnityMainThread.EnsureOrThrow("BundlePool.Despawn");
    BundlePoolFactory.Instance.Despawn(instance);
}
```

### ClearAll

```csharp
public static void ClearAll()
{
    UnityMainThread.EnsureOrThrow("BundlePool.ClearAll");
    PoolManager.Instance.ClearAll();
}
```

---

## 8. 사용 예시

### BundlePool (권장)

```csharp
// Spawn
var enemy = BundlePool.Spawn<Enemy>("Goblin", position, rotation);

// Despawn
BundlePool.Despawn(enemy);
```

### BundlePoolFactory (직접 사용)

```csharp
// Spawn (factory 확장 메서드)
var enemy = BundlePoolFactory.Instance.Spawn<Enemy>("Goblin", position, rotation);

// Despawn
BundlePoolFactory.Instance.Despawn(enemy);
```

---

## 9. 주의 사항

1. **에셋 사전 로딩 필수**: BundlePoolFactory는 AssetManager를 통해 에셋을 가져옴. 에셋이 캐시에 없으면 실패함.
2. **name은 에셋 키이자 풀 키**: 오타/대소문자 차이가 있으면 다른 풀로 분리되어 메모리 낭비 발생.
3. **메인 스레드 전용**: 모든 API는 메인 스레드에서만 호출 가능.

---

## 10. 금지 사항

- `IPoolFactory` 인터페이스 변경 금지
- `AssetManager.Instance` 사용 금지 (static 메서드 사용)
- `DestroyInstance`에서 `Destroy(instance)` 금지 (반드시 `instance.gameObject`)
- `Resources` / `Addressables` 사용 금지

---

## 11. DoD (완료 정의)

- [x] `Runtime/PoolFactories/`에 3개 파일 존재 (InspectorPoolFactory, BundlePoolFactory, BundlePool)
- [x] `BundlePoolFactory`가 `SimpleSingleton<BundlePoolFactory>` 상속
- [x] `BundlePoolFactory.GetPrefab<TAsset>(name)` Generic API 제공
- [x] `BundlePool`이 static facade로 존재하며 사용자 권장 API로 명시
- [x] `DestroyInstance`는 `00-unity-object-destruction` 규약 준수
- [x] 모든 public API에 메인 스레드 강제

---

## Reference

- Parent: `skills/devian-unity/30-unity-components/SKILL.md`
- Related: `02-pool-manager/SKILL.md` (IPoolFactory 인터페이스 정의)
- Related: `01-singleton/SKILL.md` (SimpleSingleton 베이스)
- Related: `00-unity-object-destruction/SKILL.md` (Destroy 규약)
- Related: `10-asset-manager/SKILL.md` (AssetManager API)
