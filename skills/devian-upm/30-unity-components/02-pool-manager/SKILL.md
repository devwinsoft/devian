# 02-pool-manager

Status: ACTIVE  
AppliesTo: v11  
Type: Component Specification

## 1. 목적

Unity에서 프리팹 기반 객체 재사용을 위한 PoolManager 템플릿 제공.

---

## 2. 범위

### 포함

- `IPoolable<T>` 인터페이스
- `IPoolFactory` 인터페이스
- `PoolManager` static 클래스
- `Pool<T>` 제네릭 풀 및 비제네릭 `IPool` 인터페이스
- `Spawn(string name)` + `Spawn<T>(string name)` 지원
- `GetOrCreatePool<T>()` + `GetOrCreatePool(Type, ...)`
- `Clear` / `ClearAll`
- 메인 스레드 강제 (비메인 throw)

### 제외

- 비동기 큐잉/스레드 마샬링 (throw로 단순화)
- Type당 다중 풀 (variant) — 향후 확장 항목

---

## 3. 네임스페이스

모든 생성 코드는 `namespace Devian` 고정.

---

## 4. 키/풀 규약

- **Spawn key** = `string name` (prefab의 `gameObject.name`)
- **Pool identity** = Type 당 1풀
- Type 풀 내부에서 name별 서브 큐로 분리:
  ```csharp
  Dictionary<string, Queue<T>> _inactiveByName
  ```

---

## 5. IPoolable<T> (콜백 이름 고정)

```csharp
public interface IPoolable<T> where T : UnityEngine.Component
{
    void OnPoolSpawned();
    void OnPoolDespawned();
}
```

> **금지**: `OnSpawned` / `OnDespawned` 명칭 사용 금지. 반드시 `OnPoolSpawned` / `OnPoolDespawned` 사용.

---

## 6. IPoolFactory (확장성)

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

- `GetPoolType`: prefab에서 `IPoolable<>` 구현 컴포넌트를 찾아 해당 컴포넌트 타입을 반환해야 함.

### DestroyInstance 정본 (Hard Rule)

`DestroyInstance`는 `00-unity-object-destruction/SKILL.md` 규약을 따른다:

```csharp
// 정본: DestroyInstance 구현
public void DestroyInstance(UnityEngine.Component instance)
{
#if UNITY_EDITOR
    if (!UnityEngine.Application.isPlaying)
    {
        UnityEngine.Object.DestroyImmediate(instance.gameObject);
        return;
    }
#endif
    UnityEngine.Object.Destroy(instance.gameObject);
}
```

- **컴포넌트가 아닌 `instance.gameObject`를 Destroy**
- `delete` 키워드 사용 금지 (C#에 없음)
- Runtime에서는 `UnityEngine.Object.Destroy` 사용

### 구현 계획 (문서 명시만)

| Factory | 설명 |
|---------|------|
| `InspectorPoolFactory` | 인스펙터 프리팹 링크, key = prefab.name |
| `BundlePoolFactory` | `AssetManager.Instance.GetAsset<GameObject>(name)` 사용 |

---

## 7. PoolManager API (필수)

```csharp
public static class PoolManager
{
    void Initialize(IPoolFactory factory);
    
    Pool<T> GetOrCreatePool<T>(PoolOptions options = default) where T : Component, IPoolable<T>;
    IPool GetOrCreatePool(Type type, PoolOptions options = default);
    
    T Spawn<T>(string name, Vector3 position = default, Quaternion rotation = default, Transform parent = null) where T : Component, IPoolable<T>;
    Component Spawn(string name, Vector3 position = default, Quaternion rotation = default, Transform parent = null);
    
    void Despawn(Component instance);
    
    void Clear<T>() where T : Component, IPoolable<T>;
    void Clear(Type type);
    void ClearAll();
}
```

---

## 8. 스레드 규약

- 위 API는 **전부 메인 스레드에서만 허용**
- 비메인 호출 시 `InvalidOperationException` throw
- `UnityMainThread.EnsureOrThrow(string context)` 형태로 통일

---

## 9. Clear 규약

- `Clear`는 **inactive만 Destroy** 후 큐 비움
- active 강제 회수는 제외

---

## 10. Generated Output (정본)

### 생성 대상 패키지

- `com.devian.unity`

### 생성 위치 (고정)

```
com.devian.unity/Runtime/
├── _Shared/
│   └── UnityMainThread.cs     (공용 내부 헬퍼)
└── Pool/
    ├── IPoolable.cs
    ├── IPoolFactory.cs
    ├── PoolOptions.cs
    ├── IPool.cs
    ├── Pool.cs
    └── PoolManager.cs
```

### 생성 주체 (정본)

- `framework-ts/tools/builder/build.js` 의 static UPM 처리 단계
- `processStaticUpmPackage('com.devian.unity')` 에서 staging에 생성
- 생성 순서: `_Shared` → `Singleton` → `Pool`

### 생성 파일 규칙

| 파일 | 타입 | 네임스페이스 |
|------|------|-------------|
| `IPoolable.cs` | `IPoolable<T>` | `Devian` |
| `IPoolFactory.cs` | `IPoolFactory` | `Devian` |
| `PoolOptions.cs` | `PoolOptions` (struct) | `Devian` |
| `IPool.cs` | `IPool` | `Devian` |
| `Pool.cs` | `Pool<T>` | `Devian` |
| `PoolManager.cs` | `PoolManager` (static) | `Devian` |

### 공용 헬퍼 (Singleton과 공유)

- `_Shared/UnityMainThread.cs` - 메인 스레드 검증 헬퍼
- 호출 형태: `UnityMainThread.EnsureOrThrow(string context)`
- Pool은 이 공용 헬퍼를 참조한다

### 주의사항

- **`Pool/UnityMainThread.cs`는 생성하지 않음** (공용 `_Shared/` 사용)
- 소스 레포지토리(`framework-cs/upm/com.devian.unity`)에는 Pool 폴더가 없어도 됨
- 빌드 후 `UnityExample/Packages/com.devian.unity/Runtime/Pool/*.cs` 에 파일 존재
- `Runtime/Templates/` 레거시 경로가 존재하면 FAIL

---

## Reference

- Parent: `skills/devian-upm/30-unity-components/SKILL.md`
- Related: `01-singleton-template/SKILL.md` (공용 `_Shared/UnityMainThread` 사용)
- Related: `00-unity-object-destruction/SKILL.md` (Destroy 규약)
