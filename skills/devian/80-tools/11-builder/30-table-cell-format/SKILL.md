# 30-table-cell-format

Status: ACTIVE  
AppliesTo: v10  
SSOT: skills/devian/10-module/03-ssot/SKILL.md

## Purpose

XLSX 테이블에서 `enum:*` / `class:*` 타입 컬럼의 **셀 텍스트 표현 규약(DFF)**을 정의한다.

이 문서는 “셀 문자열을 어떻게 쓴다”만 다룬다.
구체적인 파서 API와 적용 위치(빌드 단계에서 파싱 vs 런타임에서 파싱)는 Reference를 정답으로 본다.

---

## DFF 개요

DFF는 단일 셀에 **구조화된 값을 텍스트로 인코딩**하는 규약이다.

- `enum:*` / `class:*` 타입에서 사용한다
- 배열 타입(`[]`)과 결합할 수 있다

---

## 문법 (정책)

### 1) 스칼라/enum 값

- 스칼라: `123`, `3.14`, `hello`
- enum: `Member` (문자열로 작성)

### 2) 클래스(오브젝트)

키-값 쌍을 `;`로 구분한다.

```
userId=1001; displayName=Alice
```

### 3) 리스트

리스트는 `[...]` 또는 `{...}`를 쓴다.

- `[...]` : 일반 리스트 (스칼라/enum/오브젝트 모두 가능)
- `{...}` : **스칼라/enum 전용 리스트** (오브젝트 포함 금지)

예시:

```
[1,2,3]
{A,B,C}
[id=1;name=Alice, id=2;name=Bob]
```

> 오브젝트 리스트는 반드시 `[...]`를 사용한다.

---

## 이스케이프 규칙

특수 문자를 값에 포함하려면 `\`로 escape 한다.

- `\,` `\;` `\=` `\[` `\]` `\{` `\}` `\\`

예:

```
displayName=Hello\, World
```

---

## Hard Rules (MUST)

1) 오브젝트는 `key=value` 쌍으로만 표현한다.
2) 오브젝트 리스트는 `[...]`만 허용한다.
3) `{...}`는 오브젝트를 포함하면 안 된다.

### 예외: `class:CBigInt` shorthand

`class:CBigInt`는 작성 편의를 위해 전용 shorthand를 추가로 허용한다.

| 형식 | 예시 | 의미 |
|------|------|------|
| `{base, pow}` | `{5.5, 6}` | `5.5 * 10^6` |
| plain long | `1000` | `1 * 10^3` (빌드 시 정규화) |

규칙:
- 이 형식은 `class:CBigInt`에서만 허용한다.
- 일반 `class:*`에서는 `{...}`를 오브젝트 shorthand로 해석하지 않는다.
- `{base, pow}`: `pow`는 int여야 하며, 요소 개수는 정확히 2개여야 한다.
- plain long: `{}`가 없는 long 범위 정수 문자열.
- NDJSON/pb64에는 **rankKey (long)** 값으로 저장한다. 빌드 시 base/pow → rankKey 변환.

### 예외: `class:CDateTime`

`class:CDateTime`은 datetime 문자열을 입력받아 `utcTimeMs` (long)로 변환한다.

| 셀 값 | 의미 |
|------|------|
| `2024-03-08 15:30:45.123` | UTC 밀리초 long 값으로 변환 |
| `20240308153045` | 동일 (non-digit 문자 무시) |
| `2024/03/08 15:30` | 동일 (숫자만 추출) |

규칙:
- 문자열에서 숫자만 추출하여 순서대로 `year(4) → month(2) → day(2) → hour(2) → minute(2) → second(2) → millisecond(3)` 파싱.
- 자릿수가 부족하면 0으로 채운다.
- 범위를 벗어나면 유효 범위로 clamp한다.
- NDJSON/pb64에는 `utcTimeMs` long 값만 저장한다.

---

## Notes

- 현재 빌드 도구가 DFF를 즉시 파싱하지 않고 **원문 문자열을 보존**할 수 있다.
- “언제/어디서 DFF를 파싱하는가”는 구현/Reference를 따른다.

---

## Reference

- Policy SSOT: `skills/devian/10-module/03-ssot/SKILL.md`
- 동작 정본: 런타임/제너레이터 코드
