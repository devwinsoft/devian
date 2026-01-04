# Devian v10 — DFF (Class/Enum Cell Format)

Status: ACTIVE  
AppliesTo: v10  
SSOT: skills/devian/03-ssot/SKILL.md

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

---

## Notes

- 현재 빌드 도구가 DFF를 즉시 파싱하지 않고 **원문 문자열을 보존**할 수 있다.
- “언제/어디서 DFF를 파싱하는가”는 구현/Reference를 따른다.

---

## Reference

- Policy SSOT: `skills/devian/03-ssot/SKILL.md`
- 동작 정본: 런타임/제너레이터 코드