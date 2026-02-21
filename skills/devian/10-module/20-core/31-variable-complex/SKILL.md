# Devian v10 — Common Feature: Complex

Status: ACTIVE  
AppliesTo: v10  
SSOT: skills/devian/10-module/03-ssot/SKILL.md

## Overview

Complex는 **경량 마스킹(lightweight masking)** 기능을 제공한다. 보안 목적이 아닌 단순 난독화 목적이다.

주요 타입:
- CInt: 마스킹된 정수
- CFloat: 마스킹된 실수
- CString: 마스킹된 문자열
- ComplexUtil: 바이트 치환 및 Base64 인코딩 유틸리티

---

## Responsibilities

- 메모리 상의 원시 값을 단순 변환하여 보관 (마스킹)
- 직렬화(JSON, 테이블, 프로토콜) 이후에도 즉시 정상 동작
- 서버/클라이언트 공통 사용 가능 (Unity 의존 없음)

---

## Non-goals

- 보안 수준의 데이터 보호 (이것은 마스킹일 뿐)
- 키 기반 암복호화
- Unity PropertyDrawer (별도 UPM 편의 기능으로 분리)

---

## Hard Rules (MUST)

1. **Unity 의존 금지**
   - UnityEngine, UnityEditor, Mathf, Debug 등 Unity API 사용 금지
   - namespace는 `Devian` 단일 사용

2. **.NET Standard 2.1 호환**
   - Unity 타겟이므로 .NET Standard 2.1 API만 사용
   - `Random.Shared` 대신 `ComplexUtil.GetRandom()` 사용 (ThreadStatic 기반)

3. **직렬화 안전 구조**
   - CInt/CFloat: 상태는 save1, save2 두 int 필드만
   - CString: 상태는 data 문자열 필드 하나만
   - byte[] 캐시 필드 금지

4. **암호화/보안으로 표기 금지**
   - 문서/주석에 "암호화(encryption)", "보안(security)" 표현 금지
   - "마스킹(masking)", "난독화(obfuscation)" 표현 사용

---

## CInt/CFloat Permutation 규칙

save1과 save2의 바이트를 조합하여 원래 값을 복원한다.

복원 규칙 (GetValue):
- value_b0 = s1_b0 XOR s2_b0
- value_b1 = s1_b2 XOR s2_b2
- value_b2 = s1_b1 XOR s2_b1
- value_b3 = s1_b3 XOR s2_b3
- 최종 값은 little-endian 조립

저장 규칙 (SetValue):
- mask bytes(=save2 bytes)를 랜덤으로 생성
- save1 bytes는 역변환:
  - s1_b0 = value_b0 XOR s2_b0
  - s1_b1 = value_b2 XOR s2_b1
  - s1_b2 = value_b1 XOR s2_b2
  - s1_b3 = value_b3 XOR s2_b3

CFloat는 동일 규칙이며, float ↔ int bits 변환을 먼저 수행한다.

---

## CString 규칙

상태 필드: data (base64 인코딩된 마스킹 문자열)

저장 (SetValue): ComplexUtil.Encrypt_Base64(plain)으로 변환 후 data에 저장
복원 (GetValue): ComplexUtil.Decrypt_Base64(data)로 복원, 실패 시 빈 문자열 반환

---

## API 명세

### CInt

필드:
- save1: int
- save2: int

메서드:
- GetValue(): int — 복원된 정수 반환
- SetValue(int): void — 값 설정 (랜덤 마스크 생성)
- SetRaw(int, int): void — save1/save2 직접 설정 (역직렬화용)

연산자:
- implicit operator int
- implicit operator CInt

### CFloat

필드:
- save1: int
- save2: int

메서드:
- GetValue(): float — 복원된 실수 반환
- SetValue(float): void — 값 설정
- SetRaw(int, int): void — save1/save2 직접 설정

연산자:
- implicit operator float
- implicit operator CFloat

### CString

필드:
- data: string

메서드:
- GetValue(): string — 복원된 문자열 반환
- SetValue(string): void — 값 설정
- SetRaw(string): void — data 직접 설정 (역직렬화용)
- Value 프로퍼티 (get/set)

연산자:
- implicit operator string
- implicit operator CString

### ComplexUtil

정적 메서드:
- GetRandom(): Random — ThreadStatic 기반 Random 인스턴스 반환 (Unity 호환)
- Encrypt(byte[]): byte[] — 바이트 치환 마스킹
- Decrypt(byte[]): byte[] — 바이트 치환 복원
- Encrypt_Base64(string): string — 문자열을 마스킹 후 Base64 인코딩
- Decrypt_Base64(string): string — Base64 디코딩 후 마스킹 복원
- CRC32(byte[]): uint — CRC32 체크섬
- CRC32(string): uint — 문자열의 CRC32
- Encrypt_MD5(string): string — MD5 해시

---

## JSON/직렬화 표현

테이블, 프로토콜, JSON 저장 시 아래 형태를 사용한다.

CInt/CFloat:
- 형식: { "save1": int, "save2": int }
- 예시: { "save1": 1234567, "save2": 7654321 }

CString:
- 형식: { "data": string }
- 예시: { "data": "Y3+HLPJx..." }

---

## Table 빌드 시 변환 규약

엑셀 테이블에서 Complex 타입 필드를 읽을 때, 평문 입력(Plain Value)을 결정적으로 변환한다.

### 지원 입력 형식

| 입력 형식 | 예시 | 동작 |
|-----------|------|------|
| Raw JSON | `{"save1":123,"save2":456}` | 그대로 저장 |
| Plain Value | `100`, `1.5`, `hello` | 결정적 변환 |

### 결정적 변환 규칙

1. **save2(마스크) 생성**: SHA-256 해시 기반
   - seed: 시트키, PK값, 컬럼명, 평문값, 타입명 조합
   - 해시의 첫 4바이트를 little-endian int32로 사용
   - 동일 입력 → 동일 출력 보장

2. **save1 계산**: Permutation 규칙 역변환
   - CInt: 평문 정수 → permutation → save1
   - CFloat: 평문 실수 → float bits → permutation → save1

3. **CString.data**: ComplexUtil 인코딩 + Base64
   - 평문 UTF-8 bytes → Encrypt(치환) → Base64 인코딩

### 주의사항

- 이 변환은 보안 목적이 아니며, 빌드 diff 안정성을 위한 것
- CString.data는 단순 Base64가 아닌 ComplexUtil 인코딩된 Base64임
- 빌드마다 랜덤이 아닌 결정적 결과를 보장

---

## Directory Structure

C# (module — canonical):
- framework-cs/module/Devian/src/Variable/CInt.cs
- framework-cs/module/Devian/src/Variable/CFloat.cs
- framework-cs/module/Devian/src/Variable/CString.cs
- framework-cs/module/Devian/src/Variable/ComplexUtil.cs

C# (UPM — distribution):
- framework-cs/upm/com.devian.foundation/Runtime/Module/Variable/CInt.cs
- framework-cs/upm/com.devian.foundation/Runtime/Module/Variable/CFloat.cs
- framework-cs/upm/com.devian.foundation/Runtime/Module/Variable/CString.cs
- framework-cs/upm/com.devian.foundation/Runtime/Module/Variable/ComplexUtil.cs

TypeScript (core):
- framework-ts/module/devian/src/complex.ts

---

## DoD (Definition of Done)

- [x] C# Complex 폴더에 4개 파일 생성 (CInt, CFloat, CString, ComplexUtil)
- [x] UnityEngine/UnityEditor 참조 0건
- [x] CInt/CFloat에 byte[] 캐시 필드 0건
- [x] save1/save2만으로 GetValue가 즉시 정상 동작
- [x] UnityExample 패키지에 동일 파일 존재
- [x] TS complex.ts 생성
- [x] TS features/index.ts에 complex export 추가
- [x] SKILL 문서 추가 (코드 블록 최소화)

---

## Unity Inspector 노출 정책

CInt/CFloat/CString의 내부 상태(save1/save2/data)는 Inspector에서 직접 노출하지 않는다.

| 항목 | 정책 |
|------|------|
| 표시 | GetValue()로 복호화된 값만 표시 |
| 편집 | 변경 시 SetValue()로 내부 상태 갱신 |
| 담당 | `com.devian.foundation/Editor/Complex/`의 PropertyDrawer |
| 위치 | `com.devian.foundation/Editor/Complex/`에 위치 (Devian.Unity.Editor 어셈블리) |

PropertyDrawer 파일:
- `CIntPropertyDrawer.cs`
- `CFloatPropertyDrawer.cs`
- `CStringPropertyDrawer.cs`

> **주의**: 문서/주석에서 "암호화/보안" 표현 금지. "마스킹/인코딩" 용어 사용.

---

## Reference

- CBigInt Skill: skills/devian/10-module/20-core/35-variable-bigint/SKILL.md
- Unity Components Index: skills/devian-unity/10-foundation/SKILL.md
- Foundation Package SSOT: skills/devian/10-module/03-ssot/SKILL.md
- Domain Common Package: skills/devian-unity/06-domain-packages/com.devian.domain.common/SKILL.md
