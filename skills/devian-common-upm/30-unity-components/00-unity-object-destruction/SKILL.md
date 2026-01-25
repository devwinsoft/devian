# 00-unity-object-destruction

Status: ACTIVE  
AppliesTo: v11  
Type: Hard Rule / Convention

## 1. 목적

Unity 오브젝트 삭제(Destroy) 규약을 정본화하여 일관된 코드 생성 및 Claude의 실수 방지.

---

## 2. Hard Rules

### 2.1 C#에 delete는 없다

- **C#에는 `delete` 키워드가 없다**
- Unity 오브젝트 삭제는 항상 `UnityEngine.Object.Destroy(...)` 사용
- `delete`를 사용하는 코드 생성 금지

### 2.2 Runtime 삭제 규약

```csharp
// 정본: Runtime에서 오브젝트 삭제
UnityEngine.Object.Destroy(targetGameObjectOrComponent);
```

### 2.3 DestroyImmediate 사용 조건 (Hard Rule)

`DestroyImmediate`는 **기본 금지**. 아래 조건에서만 허용:

```csharp
#if UNITY_EDITOR
if (!Application.isPlaying)
{
    UnityEngine.Object.DestroyImmediate(target);
}
#endif
```

허용 조건:
- `#if UNITY_EDITOR` 블록 내부
- `Application.isPlaying == false` 일 때

### 2.4 컴포넌트 삭제 정책 (Hard Rule)

컴포넌트를 받았을 때:
- **컴포넌트만 삭제 금지**
- **`instance.gameObject`를 Destroy** (풀/싱글톤에서 일관성 유지)

---

## 3. 정본 코드

### 3.1 DestroyGameObject 헬퍼 (정본)

```csharp
public static void DestroyGameObject(UnityEngine.Component instance)
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

### 3.2 사용 예시

```csharp
// ✅ 올바른 사용
UnityEngine.Object.Destroy(instance.gameObject);

// ✅ 올바른 사용 (헬퍼)
DestroyGameObject(instance);

// ❌ 금지: 컴포넌트만 삭제
UnityEngine.Object.Destroy(instance);  // 컴포넌트만 삭제됨

// ❌ 금지: Runtime에서 DestroyImmediate
UnityEngine.Object.DestroyImmediate(instance.gameObject);  // Editor 전용

// ❌ 금지: delete 사용 (C#에 없음)
delete instance;  // 컴파일 에러
```

---

## 4. 적용 대상

이 규약은 다음 템플릿/컴포넌트에 적용된다:

| 컴포넌트 | 적용 위치 |
|----------|-----------|
| Singleton | 중복 인스턴스 처리 시 |
| PoolManager | `Pool.Clear()`, `IPoolFactory.DestroyInstance()` |
| AssetManager | 번들 언로드 시 (해당되는 경우) |

---

## 5. FAIL 조건

- `delete` 키워드 사용
- Runtime에서 `DestroyImmediate` 사용 (`#if UNITY_EDITOR` 없이)
- 컴포넌트만 삭제 (`instance.gameObject` 대신 `instance` 삭제)
- `Destroy(...)` 대신 축약 호출 (`UnityEngine.Object.` 생략 시 명확성 저하)

---

## Reference

- Parent: `skills/devian-common-upm/30-unity-components/SKILL.md`
- Used by: `01-singleton-template/SKILL.md`, `02-pool-manager/SKILL.md`
