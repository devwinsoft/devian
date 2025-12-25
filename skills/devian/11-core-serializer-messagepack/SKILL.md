# Devian – 11 Core Serializer: MessagePack

## Purpose

**MessagePack 직렬화 구현 모듈을 제공한다.**

Devian Core Runtime(`Devian.Core`)에서 정의한 `IEntityCodec` 추상화를 기반으로,  
Entity가 플랫폼/전송수단과 무관하게 바이너리로 마샬링/언마샬링될 수 있도록 한다.

---

## Belongs To

**Core Runtime**

> Serializer는 Core의 **확장 옵션(Skill)**이지,  
> contracts나 도메인 규칙이 아니다.

---

## Scope

### In Scope

| 항목 | 설명 |
|------|------|
| MessagePack 기반 직렬화/역직렬화 | `IEntityCodec` 구현 |
| Core extension으로서의 serializer | 선택적 확장 모듈 |
| Options policy | 플랫폼별 설정 |

### Out of Scope (Skill 영역)

| 항목 | 담당 Skill |
|------|-----------|
| table schema 정의 | contracts 영역 |
| contracts 구조 | 가정하지 않음 |
| server transport | Server Skill |
| application lifecycle | 각 플랫폼 Skill |

---

## Hard Rules (MUST)

| # | Rule |
|---|------|
| 1 | Serializer는 **optional component**이다 |
| 2 | Serializer는 contracts 구조를 **가정하지 않는다** |
| 3 | `Devian.Core`는 MessagePack을 **직접 참조하지 않는다** |

---

## Soft Rules (SHOULD)

| # | Rule |
|---|------|
| 1 | 플랫폼별 옵션 차이를 허용 (Unity vs Server) |
| 2 | "안전하고 예측 가능한" 기본 설정 사용 |

---

## Implementation

### MessagePack Codec

```csharp
public class MessagePackEntityCodec : IEntityCodec
{
    public byte[] Serialize<T>(T obj);
    public T Deserialize<T>(byte[] data);
}
```

### Options Policy

```csharp
public static class MessagePackOptionsFactory
{
    public static MessagePackSerializerOptions CreateDefault();
    public static MessagePackSerializerOptions CreateForUnity();
    public static MessagePackSerializerOptions CreateForServer();
}
```

---

## Directory & Project Constraints

### Project Location

```
devian/
├── framework/
│   └── cs/
│       └── Devian.Core.Serializer.MessagePack/   ← 여기
└── Devian.sln
```

> ⚠️ `packages/` 용어는 사용하지 않는다 (Unity `Packages/`와 혼동 방지)

### Project References

- `Devian.Core.Serializer.MessagePack` → `Devian.Core` ✅
- `Devian.Core` → `Devian.Core.Serializer.MessagePack` ❌

### Allowed Dependencies

- `MessagePack` NuGet 패키지

### Forbidden Dependencies

- UnityEngine.*
- 서버 전용 런타임

---

## Responsibilities

1. **`IEntityCodec` 구현 제공** — MessagePack 기반 직렬화
2. **Core 격리 유지** — MessagePack 참조가 이 프로젝트에만 존재
3. **플랫폼 독립성** — Unity/Server 모두 사용 가능

---

## Acceptance Criteria

| # | 조건 |
|---|------|
| 1 | "이걸 써야 Devian을 쓸 수 있다"는 인상이 없다 |
| 2 | Core runtime의 필수 요소처럼 보이지 않는다 |
| 3 | MessagePack 참조가 이 프로젝트에만 존재 |
| 4 | `Devian.Core`는 MessagePack을 모른다 |

---

## Related Skills

| Skill | 관계 |
|-------|------|
| `10-core-runtime` | **확장** — `IEntityCodec` 구현 |
| `12-common-standard-types` | **참조** — 공용 타입 |

---

## Version History

| Version | Date | Changes |
|---------|------|---------|
| 1.0.0 | 2024-12-25 | **디렉토리 정책**: `packages/` → `framework/cs/` |
| 0.2.0 | 2024-12-21 | 표준 템플릿 적용, optional 강조 |
| 0.1.0 | 2024-12-20 | Initial skill definition |
