# Devian v10 — Variable: Variant

Status: ACTIVE
AppliesTo: v10
SSOT: skills/devian-core/03-ssot/SKILL.md

## Purpose

Variant는 Int/Float/String 중 하나의 값을 담는 Tagged Union 타입이다.
Excel 입력과 NDJSON 저장 형식을 정의한다.

---

## Excel 입력 형식 (정본)

**접두사 필수, 추론 금지.**

| 입력 | 의미 |
|------|------|
| `i:123` | 정수 123 |
| `f:3.14` | 실수 3.14 |
| `s:가나다` | 문자열 "가나다" |

### 규칙

- 접두사 `i:`, `f:`, `s:` 중 하나 필수
- 접두사 없이 입력하면 **에러** (추론 금지)
- 빈 셀은 `null`

### 타입 검증

- `i:` 뒤에는 정수만 허용 (소수점 불가)
- `f:` 뒤에는 숫자(정수/실수) 허용
- `s:` 뒤에는 임의 문자열 허용 (빈 문자열 `s:` 허용)

---

## NDJSON 저장 형식 (정본)

**키 하나만 있는 오브젝트.**

| 타입 | JSON |
|------|------|
| Int | `{"i": 123}` |
| Float | `{"f": 3.14}` |
| String | `{"s": "가나다"}` |

### 규칙

- 키는 정확히 **하나**만 있어야 한다 (`i`, `f`, `s` 중 하나)
- 다른 키가 섞이면 **실패**
- `i` 값은 정수만 허용
- `f` 값은 number 허용
- `s` 값은 string만 허용

### 예시

```json
{"i": 42}
{"f": 3.14159}
{"s": "Hello World"}
{"s": ""}
```

### 금지 형식

```json
{"i": 1, "f": 2}       // 키 2개 - 실패
{"k": "i", "i": 123}   // k 키 불필요 - 실패
{"i": 3.14}            // i에 실수 - 실패
{"f": "abc"}           // f에 문자열 - 실패
```

---

## 라운드트립 보장

| Excel 입력 | NDJSON | 복원 값 |
|------------|--------|---------|
| `i:123` | `{"i":123}` | 123 (int) |
| `f:3.14` | `{"f":3.14}` | 3.14 (float) |
| `s:가나다` | `{"s":"가나다"}` | "가나다" (string) |

Serialize → Deserialize 후 값이 **동일**해야 한다.

---

## Directory Structure

C# (module — canonical):
- `framework-cs/module/Devian/src/Variable/Variant.cs`

C# (UPM — com.devian.foundation / Devian.Core):
- `framework-cs/upm/com.devian.foundation/Runtime/Module/Variable/Variant.cs`
- `framework-cs/apps/UnityExample/Packages/com.devian.foundation/Runtime/Module/Variable/Variant.cs`

TypeScript:
- `framework-ts/module/devian/src/variant.ts` (타입)
- `framework-ts/tools/builder/generators/table.js` (파서)

---

## 금지 행동

- 접두사 없이 값 추론 금지
- Complex 형식(save1/save2) 저장 금지 (실제 값 저장)
- k 키 사용 금지 (단순 형식)
- 키 2개 이상 허용 금지

---

## Reference

- SSOT: 이 파일 (`skills/devian-core/32-variable-variant/SKILL.md`)
