# 45-tablegen-enumgen

Status: ACTIVE  
AppliesTo: v10  
SSOT: skills/devian-core/03-ssot/SKILL.md

## Overview

DATA 테이블(예: XLSX)의 Options Row(3번째 줄)에서 `gen:<EnumName>` 옵션을 선언하면:

1. 해당 컬럼(gen 컬럼)이 **PK 컬럼**이 된다 (gen 컬럼 = PK 컬럼).
2. 해당 컬럼의 각 row 값이 **enum 멤버 이름(name)** 이 된다.
3. enum 멤버 값(value)은 **결정적 자동 할당** (이름 오름차순 정렬 후 0부터 순차).
4. TB 캐시에 `Find(<EnumName> key)` / `TryFind(<EnumName> key)` **헬퍼가 자동 생성**된다.

---

## Terminology

| 용어 | 설명 |
|------|------|
| Options Row | XLSX 3번째 줄 (열별 옵션 정의) |
| Gen Column | `gen:<EnumName>` 옵션이 선언된 컬럼 (= PK 컬럼, enum NAME source) |

---

## Hard Rules (MUST)

### 1. Gen Column = PK Column

- `gen:<EnumName>` 옵션이 붙은 컬럼은 **반드시 `pk`여야 한다**.
- 즉, gen 컬럼 = PK 컬럼 (같은 컬럼).
- gen 컬럼에 `pk`가 없으면 빌드 **FAIL**.
- gen 컬럼은 테이블당 **딱 1개**만 허용한다 (2개 이상 FAIL).

### 2. Enum Member Name = Gen/PK Column Value

- gen/PK 컬럼의 각 row 값이 enum 멤버 이름이 된다.
- 멤버 이름은 유효한 식별자여야 한다 (`^[A-Za-z_][A-Za-z0-9_]*$`).

### 3. Enum Member Value = Deterministic Auto-Assignment

enum 멤버 값은 **결정적 자동 할당**으로 생성된다:

1. 모든 멤버 이름을 **오름차순(ASCII) 정렬**한다.
2. 정렬된 순서대로 **0부터 1씩 증가하는 int 값**을 할당한다.

예시:
```
테이블 데이터: CriRate, AttackPower, MaxHealth
정렬 후:       AttackPower, CriRate, MaxHealth
할당 결과:     AttackPower=0, CriRate=1, MaxHealth=2
```

### 3.1 Enum Member Value = code(int) (Opt-in)

Options Row(3번째 줄)에서 gen 컬럼(=PK 컬럼)에 `code` 키워드를 함께 선언하면:

- 예: `pk, gen:ServerErrorType, code`
- 이 경우 enum 멤버 value는 0..N-1 자동 할당이 아니라, **같은 row의 `code` 컬럼(int) 값**을 사용한다.
- 출력 순서는 여전히 **멤버 이름 오름차순(결정적)** 으로 유지한다.

Failure Conditions (MUST fail):
- `code` 컬럼이 없으면 FAIL
- `code` 타입이 int가 아니면 FAIL
- row의 `code`가 비어있으면 FAIL
- `code` 값이 중복이면 FAIL

### 4. 식별자 규칙 위반은 실패

- gen 컬럼 값(enum 멤버 이름)이 C#/TS 식별자 규칙을 위반하면 빌드 실패한다.
- 정규식: `^[A-Za-z_][A-Za-z0-9_]*$`
- 자동 치환(공백→_, 특수문자 제거) 같은 "추측 기반 보정"은 **금지**한다.

### 5. 중복은 실패

- enum 멤버 이름(gen 컬럼 값) 중복: **FAIL**

### 6. 결정적 출력

- enum 멤버 나열 순서는 **이름 오름차순**으로 고정한다.
- 동일 입력에서 동일 출력이 보장되어야 한다.

### 7. Find/TryFind 헬퍼

- `TB_<n>.Find(<EnumName> key)` / `TryFind(<EnumName> key)` 헬퍼가 생성된다.
- keyType이 이미 `enum:<EnumName>`인 경우, 중복 오버로드는 생성하지 않는다.
- Find/TryFind는 gen 테이블에만 생성된다.

---

## Table Authoring Spec

### Required Columns

| 역할 | 컬럼명 | Type (권장) | Options | 설명 |
|------|--------|-------------|---------|------|
| PK + Enum Name | (자유) | `enum:<EnumName>` | `pk, gen:<EnumName>` | PK이자 enum 멤버 이름 source |

### Example: TB_COMPLEX_POLICY

```
Row 1 (Header):    key                      | fallbackValue | minValue | maxValue
Row 2 (Type):      enum:ComplexPolicyType   | Variant       | Variant  | Variant
Row 3 (Options):   pk, gen:ComplexPolicyType |              |          |
Row 4 (Comment):   (ignored)
Row 5+ (Data):     AttackPower              | i:0           | i:0      | i:10000
                   CriRate                  | f:0           | f:0      | f:3.5
                   MaxHealth                | i:100         | i:1      | i:99999
```

여기서:
- `key` 컬럼 = PK = gen 컬럼 (같은 컬럼, 옵션: `pk, gen:ComplexPolicyType`)
- `key` 컬럼의 값이 enum 멤버 이름
- enum 값은 자동 할당: `AttackPower=0`, `CriRate=1`, `MaxHealth=2` (이름 정렬 순)
- **Variant 컬럼은 NDJSON에서 `{k, i|f|s}` 오브젝트로 저장되며, `i`/`f`/`s`는 Complex shape(CInt/CFloat/CString)이다.**

---

## Output Artifacts

### C# Enum

```csharp
namespace Devian.Domain.Common
{
    public enum ComplexPolicyType : int
    {
        AttackPower = 0,
        CriRate = 1,
        MaxHealth = 2,
    }
}
```

### C# TB Cache Find/TryFind

```csharp
public static class TB_COMPLEX_POLICY
{
    // 기존 Get/TryGet 유지...

    // Find/TryFind - keyType이 enum이므로 기본 버전만 생성
    public static Row Find(ComplexPolicyType key) { ... }
    public static bool TryFind(ComplexPolicyType key, out Row? row) { ... }
}
```

### TypeScript Enum

```typescript
export enum ComplexPolicyType {
    AttackPower = 0,
    CriRate = 1,
    MaxHealth = 2,
}
```

### TypeScript TB Cache find/tryFind

```typescript
export class TB_COMPLEX_POLICY {
    // 기존 get/tryGet 유지...

    // keyType이 enum이므로 기본 버전만
    static find(key: ComplexPolicyType): Row { ... }
    static tryFind(key: ComplexPolicyType): Row | undefined { ... }
}
```

---

## Failure Conditions (Build MUST fail)

| 조건 | 에러 메시지 예시 |
|------|-----------------|
| gen 컬럼이 2개 이상 | `[gen:] Table '...': only one gen column allowed per table` |
| gen 컬럼에 pk가 없음 | `[gen:] Table '...': gen field must be pk (gen column is the PK)` |
| EnumName이 비어있음 | `[gen:] Table '...': enum name required` |
| EnumName이 식별자 규칙 위반 | `[gen:] Table '...': invalid enum name '...'` |
| enum 멤버 이름이 식별자 규칙 위반 | `[gen:] Table '...': invalid member name '...'` |
| enum 멤버 이름 중복 | `[gen:] Table '...': duplicate member name '...'` |
| row의 gen 컬럼 값이 비어있음 | `[gen:] Table '...': empty enum member name at row ...` |
| key:true 또는 key 옵션 사용 | `Option 'key' is not supported. Use 'pk'.` |

---

## DoD (Definition of Done)

- [x] `gen:<EnumName>` 옵션이 Options Row에서 파싱된다
- [x] gen 컬럼은 반드시 `pk`여야 한다 (gen = PK)
- [x] gen/PK 컬럼 값이 enum 멤버 이름으로 사용된다
- [x] enum 멤버 값이 이름 정렬 + 0..N-1 자동 할당된다
- [x] enum 코드(C#/TS)가 생성된다
- [x] enum 멤버 순서가 이름 오름차순으로 결정적이다
- [x] TB 캐시에 Find/TryFind 헬퍼가 생성된다 (gen 테이블만)
- [x] keyType이 enum이면 중복 오버로드를 생성하지 않는다
- [x] 식별자 위반/중복 시 빌드가 실패한다
- [x] key:true 또는 key 옵션 사용 시 빌드가 실패한다
- [x] 기존 테이블(gen 미사용)은 영향 없다

---

## Reference

- Table Authoring Rules: `skills/devian-builder/30-table-authoring-rules/SKILL.md`
- TableGen Implementation: `skills/devian-builder/42-tablegen-implementation/SKILL.md`
- Variant Feature: `skills/devian-core/32-variable-variant/SKILL.md`
