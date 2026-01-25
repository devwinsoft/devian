# 04-pool-factories

Status: ACTIVE  
AppliesTo: v11  
Type: Component Specification

## 1. 목적

`IPoolFactory`의 실사용 구현 2종 제공:
- `InspectorPoolFactory` - 인스펙터에서 프리팹 등록
- `BundlePoolFactory` - AssetBundle 기반 로딩

---

## 2. 네임스페이스

모든 생성 코드는 `namespace Devian` 고정.

---

## 3. 생성 대상 패키지

- `com.devian.unity`

---

## 4. 생성 위치 (정본)

```
com.devian.unity/Runtime/PoolFactories/
├── InspectorPoolFactory.cs
└── BundlePoolFactory.cs
```

---

## 5. 생성 주체 (정본)

- `framework-ts/tools/builder/build.js`
- `processStaticUpmPackage('com.devian.unity')` 단계에서 staging에 생성
- 생성 순서: `_Shared` → `Singleton` → `Pool` → `PoolFactories`

---

## 6. InspectorPoolFactory 규약

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
- `Destroy(instance)` 금지

---

## 7. BundlePoolFactory 규약

형태: `public sealed class BundlePoolFactory : IPoolFactory`

### GetPrefab(name)

```csharp
return AssetManager.GetAsset<GameObject>(name);
```

- `AssetManager.Instance` 금지 (존재하지 않는 API)
- `Resources.Load` / `Addressables` 금지

### GetPoolType/CreateInstance/DestroyInstance

`InspectorPoolFactory`와 동일 규약.

---

## 8. 금지 사항

- `IPoolFactory` 인터페이스 변경 금지
- `AssetManager.Instance` 사용 금지 (static 메서드 사용)
- `DestroyInstance`에서 `Destroy(instance)` 금지 (반드시 `instance.gameObject`)
- `Resources` / `Addressables` 사용 금지

---

## 9. DoD (완료 정의)

- [ ] 빌드 후 `Runtime/PoolFactories/`에 2개 파일 존재
- [ ] 두 클래스 모두 `IPoolFactory` 구현
- [ ] `DestroyInstance`는 `00-unity-object-destruction` 규약 준수
- [ ] `BundlePoolFactory`는 `AssetManager.GetAsset<GameObject>(name)` 사용

---

## Reference

- Parent: `skills/devian-upm/30-unity-components/SKILL.md`
- Related: `02-pool-manager/SKILL.md` (IPoolFactory 인터페이스 정의)
- Related: `00-unity-object-destruction/SKILL.md` (Destroy 규약)
- Related: `10-asset-manager/SKILL.md` (AssetManager API)
