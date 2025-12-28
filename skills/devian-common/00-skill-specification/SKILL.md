# Devian Common – 00 Skill Specification

## Purpose

**이 스킬은 Devian.Common / devian-common 모듈 및 Common 도메인에 대한 규약을 정의한다.**

---

## 1. Devian.Common의 역할

Devian.Common은 다음을 제공한다.

- **공용 도메인**: 모든 도메인이 참조할 수 있는 타입/스키마의 원천
- **공용 타입**: `G*` prefix 표준 타입 (GFloat2, GFloat3, GColor32 등)
- **공용 런타임 모듈**: 빌드 산출물 + manual 코드를 결합한 배포 단위

---

## 2. C#/TS 경로 정본

### C# 모듈 구조

```
framework/cs/Devian.Common/
├── generated/   # Common 도메인 빌드 결과 (기계 생성, 커밋)
└── manual/      # 개발자 작성 코드 (생성기 덮어쓰기 금지)
```

### TypeScript 모듈 구조

```
framework/ts/devian-common/
├── generated/   # Common 도메인 빌드 결과 (기계 생성, 커밋)
└── manual/      # 개발자 작성 코드 (생성기 덮어쓰기 금지)
```

### 소유권 규칙 (MUST)

| 폴더 | 소유권 | 규칙 |
|------|--------|------|
| `generated/` | 기계 | Common 도메인 빌드 결과만. 사람이 수정 금지. **커밋 필수** |
| `manual/` | 사람 | 개발자 직접 작성. 생성기 **덮어쓰기 금지** |

---

## 3. Common 도메인 참조 규약

**수동 작성 proto 폴더(proto-manual)는 폐기되었다.**

### 도메인 폴더 구조 (v9)

```
input/Common/
├── contracts/     # 계약 정의 (*.json)
└── tables/        # 테이블 정의 (*.xlsx)
```

> Common 도메인은 **Data domain**으로서 contracts와 tables만 가진다.

### ref:Common.{Name} 규칙

| 참조 | 허용 | 비고 |
|------|:----:|------|
| `ref:Common.{Name}` | ✅ | 다른 도메인이 Common 참조 |
| `ref:OtherDomain.{Name}` | ❌ | 1단계에서 금지 |
| Common → 다른 도메인 | ❌ | 의존 역전 금지 |

---

## 4. 도메인 vs 모듈 (중요)

| 구분 | 설명 |
|------|------|
| **Common 도메인** | 스키마/데이터/타입의 출처 (Data domain) |
| **Devian.Common 모듈** | Common 도메인 빌드 산출물 + manual 코드 결합. 런타임 의존 대상 |

> **도메인(Common)**과 **모듈(Devian.Common)**은 동일하지 않다. 모듈은 도메인의 산출물을 담는 그릇이다.

---

## 5. 네이밍 정책 (요약)

| 접근자 | 네이밍 규칙 |
|--------|------------|
| internal / 내부 전용 public | `_MethodName` |
| private / protected | `methodName` (소문자 시작) |

---

## 6. 공용 타입 정책

공용 타입(`G*` prefix)은 Common에서 관리한다.

| 타입 | 설명 |
|------|------|
| `GFloat2`, `GFloat3` | 플랫폼 독립적 벡터 |
| `GColor32` | 32비트 RGBA 색상 |
| `GEntityId`, `GStringId` | 범용 식별자 |

> 상세: `skills/devian-common/12-common-standard-types/SKILL.md`

---

## Hard Rules (MUST)

| # | 규칙 |
|---|------|
| 1 | `Devian.Common`은 어떤 Devian 모듈도 참조하지 않는다 (최하위 계층) |
| 2 | `G*` prefix 타입은 `Devian.Common`에만 정의한다 |
| 3 | generated는 기계 소유, manual은 사람 소유 |
| 4 | Common → 다른 도메인 참조 금지 (의존 역전) |

---

## Related Skills

| Skill | 관계 |
|-------|------|
| `70-common-domain-policy` | Common 도메인 기본 정책 |
| `12-common-standard-types` | 공용 표준 타입 정의 |

---

## Version History

| Version | Date | Changes |
|---------|------|---------|
| 1.0.0 | 2025-12-28 | Initial |
