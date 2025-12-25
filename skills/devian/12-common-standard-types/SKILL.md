# Devian – 12 Common Standard Types

## Purpose

**Defines shared standard types used across generated contracts and consumers.**

`Devian.Common`은 플랫폼 독립적인 **표준 타입 저장소**다.

- 범용 값 타입 (`GFloat2`, `GFloat3`, `GColor32`)
- 범용 ID 타입 (`GEntityId`, `GStringId`)
- 범용 enum / flags (필요 시)

이 타입들은 도메인 contracts에서 재사용되고, Protocol / Tables 어디서든 참조 가능하다.

---

## Scope

### In Scope

| 항목 | 설명 |
|------|------|
| 공통 scalar 타입 | `GFloat2`, `GFloat3`, `GColor32` |
| 공통 ID 타입 | `GEntityId`, `GStringId` |
| 공통 enum / flags | 필요 시 추가 |

### Out of Scope

| 항목 | 설명 |
|------|------|
| runtime behavior | ❌ 로직 포함 안 함 |
| serialization policy | ❌ 직렬화 정책 없음 |
| server implementation | ❌ 서버 코드 없음 |
| 도메인 특화 타입 | ❌ ItemId, QuestType 등 |

---

## Hard Rules (MUST)

| # | Rule |
|---|------|
| 1 | `Devian.Common`은 어떤 Devian 모듈도 참조하지 않는다 (최하위 계층) |
| 2 | `G*` prefix 타입은 `Devian.Common`에만 정의한다 |
| 3 | 도메인 contracts는 `G*` 타입을 **사용만 하고, 정의하지 않는다** |

### G Prefix Rule

> `G*`는 **Devian이 제공하는 표준 공용 타입**을 의미한다.  
> hand-written이든 codegen이든 상관없이 `Devian.Common`에 위치한다.

---

## Soft Rules (SHOULD)

| # | Rule |
|---|------|
| 1 | `readonly struct`로 불변 값 타입 정의 |
| 2 | `IEquatable<T>` 구현 |
| 3 | 간단한 생성자와 연산자만 제공 |

---

## Directory & Project Structure

### Framework vs Modules 역할

| 디렉토리 | 역할 |
|----------|------|
| `framework/` | 언어별 Core 라이브러리 (Devian.Common 포함) |
| `modules/` | 빌드가 생성하는 도메인 산출물 |

> ⚠️ `packages/` 용어는 사용하지 않는다 (Unity `Packages/`와 혼동 방지)

### C# Common 구조

```
devian/
├── framework/
│   └── cs/
│       └── Devian.Common/
│           ├── Devian.Common.csproj
│           └── src/
│               ├── Math/
│               │   ├── GFloat2.cs
│               │   ├── GFloat3.cs
│               │   └── GColor32.cs
│               ├── Primitives/
│               │   └── GEntityId.cs
│               └── IsExternalInit.cs  ← 폴리필 (여기에만)
└── Devian.sln
```

### Namespace Convention

```
Devian.Common    ← 단일 네임스페이스
```

---

## Implemented Types

### Math Types

| 타입 | 설명 |
|------|------|
| `GFloat2` | 플랫폼 독립적 2D 벡터 (float X, Y) |
| `GFloat3` | 플랫폼 독립적 3D 벡터 (float X, Y, Z) |
| `GColor32` | 플랫폼 독립적 32비트 RGBA 색상 (byte R, G, B, A) |

### Primitive Types

| 타입 | 설명 |
|------|------|
| `GEntityId` | long 기반 범용 엔티티 식별자 |
| `GStringId` | string 기반 식별자 (GUID 등) |

---

## Dependency Rules

### Devian.Common은 최하위 계층

```
┌────────────────┐
│ Devian.Common  │  ← 어떤 Devian 모듈도 참조하지 않음
└────────────────┘
        ↑
        │ 참조
┌────────────────┐
│  Devian.Core   │
│  Devian.Tools  │
└────────────────┘
```

### Allowed Dependencies

- **없음** (System 네임스페이스만 사용)

### Forbidden Dependencies

- Devian.Core 참조 금지
- MessagePack / Json.NET / UnityEngine 등 외부 의존 금지
- 플랫폼 특화 코드 금지

---

## Unity Conversion (Unity Skill 영역)

Devian.Common은 Unity를 모른다. Unity 변환은 **Unity Skill**에서 제공한다.

```csharp
// Unity Skill에서 제공
public static class DevianUnityExtensions
{
    public static UnityEngine.Vector3 ToUnity(this GFloat3 v) 
        => new UnityEngine.Vector3(v.X, v.Y, v.Z);
    
    public static GFloat3 ToDevian(this UnityEngine.Vector3 v) 
        => new GFloat3(v.x, v.y, v.z);
}
```

---

## Responsibilities

1. **공용 값 타입 정의** — 모든 도메인에서 재사용
2. **플랫폼 독립성 보장** — Unity/Web/Server 어디서든 사용 가능
3. **codegen / Skill 참조용** — 다른 스킬 문서에서 참조

---

## Acceptance Criteria

| # | 조건 |
|---|------|
| 1 | 이 문서가 "기반 라이브러리"처럼 보이지 않는다 |
| 2 | codegen / Skill 문서에서 참조 전용으로 사용 가능 |
| 3 | 모든 타입이 `G*` prefix 사용 |
| 4 | 다른 Devian 모듈 참조 없음 |
| 5 | Unity/Web/DB 의존 없음 |

---

## Related Skills

| Skill | 관계 |
|-------|------|
| `10-core-runtime` | **사용** — Core가 Common 참조 |
| `00-rules-minimal` | **규칙** — 모듈 구조 |
| `21-codegen-table` | **사용** — generated 코드가 G* 타입 참조 |

---

## Version History

| Version | Date | Changes |
|---------|------|---------|
| 1.0.0 | 2024-12-25 | **디렉토리 정책**: `packages/` → `framework/cs/` |
| 0.2.0 | 2024-12-21 | 표준 템플릿 적용, 용어 정리 |
| 0.1.0 | 2024-12-21 | Initial skill definition |
