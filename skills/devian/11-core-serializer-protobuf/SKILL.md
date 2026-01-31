# Devian v10 — Serializer / Protobuf Policy

Status: ACTIVE  
AppliesTo: v10  
SSOT: skills/devian/03-ssot/SKILL.md

## Purpose

Devian v10에서 사용하는 직렬화 정책(Encoding/Decoding)을 정의한다.

이 문서는 **"무슨 포맷을 지원/금지하는가"**만 서술한다.
구체적인 바이트 레이아웃/런타임 API는 **런타임/제너레이터 코드**를 정답으로 본다.

---

## Supported Formats (Policy)

### 1) JSON

- 디버깅/툴링을 위한 기본 교환 포맷
- Protocol/Contracts/NDJSON 데이터 모두 JSON 계열을 사용한다

### 2) Protobuf-style Wire (Devian)

- v10은 ".proto → protoc" 체인을 사용하지 않는다
- 대신 Protobuf wire 개념을 차용한 **Devian 전용 인코더/디코더**를 사용한다
- 목적: 작은 payload, 빠른 파싱, Span 기반 처리

---

## Framework Modules

### C# (Devian — 단일 모듈)

> Devian C# 런타임은 단일 모듈(`Devian.csproj`)로 통합되어 있다.
> **Protobuf 관련 타입은 `namespace Devian`에 위치한다.** (분리된 하위 네임스페이스 금지)
> 타입명(`Dff*`, `Protobuf*`, `IProto*`)은 기존 이름을 유지한다.

| 파일 | namespace | 역할 |
|------|-----------|------|
| `DffValue.cs` | `Devian` | DFF 값 타입 정의 |
| `DffParser.cs` | `Devian` | DFF 문법 파싱 |
| `DffConverter.cs` | `Devian` | 셀 문자열 → DffValue 변환 |
| `DffOptions.cs` | `Devian` | DFF 파싱 옵션 |
| `DffProtobuf.cs` | `Devian` | DFF → Protobuf IMessage 변환 API |
| `DffProtobufBuilder.cs` | `Devian` | Descriptor 기반 IMessage 빌드 |
| `IProtoEntity.cs` | `Devian` | Protobuf 엔티티 인터페이스 |
| `ProtobufEntityConverter.cs` | `Devian` | Protobuf 엔티티 변환 |

### TypeScript (@devian/core)

> Devian TS 런타임은 단일 패키지(`@devian/core`)로 통합되어 있다.
> Protobuf 관련 타입은 `@devian/core/proto`에서 export된다.

| 파일 | 역할 |
|------|------|
| `DffValue.ts` | DFF 값 타입 정의 |
| `DffConverter.ts` | 셀 문자열 → DffValue 변환 |
| `ProtobufCodec.ts` | Protobuf encode/decode, JSON fallback |
| `IProtoEntity.ts` | Protobuf 엔티티 인터페이스 |

---

## DFF (Data Field Format)

XLSX에서 `enum:*` / `class:*` 타입 셀에 들어가는 텍스트 표현을 **DFF**로 정의한다.

- DFF는 "셀 텍스트 → 런타임 오브젝트" 변환 규약이다
- 빌드 도구가 DFF를 항상 해석해야 한다고 강제하지 않는다
  - (예) 빌드 산출 NDJSON에 셀 원문을 보존하고, 런타임 로더에서 DFF를 해석할 수 있다

DFF 문법/예시는 `skills/devian/31-class-cell-format/SKILL.md`를 따른다.

---

## MUST / MUST NOT

MUST

- 인코딩/디코딩은 **결정적**이어야 한다(같은 입력 → 같은 바이트/같은 객체)
- Protocol의 opcode/tag 정책(SSOT)을 위반하면 실패해야 한다

MUST NOT

- .proto 파일을 입력 정본으로 취급하지 않는다
- "편의상 임의 타입 매핑/임의 필드 스킵" 같은 묵시적 처리 금지

---

## Unity Google.Protobuf.dll 정책 (Hard Rule)

Unity 프로젝트에서 Protobuf 기반 기능(`DffProtobuf`, `IProtoEntity` 등)을 사용하려면 **Google.Protobuf.dll**이 필수이다.

### 필수 조건

1. `Google.Protobuf.dll` 파일이 Unity가 로드하는 위치에 존재해야 한다
   - 예: `Assets/Plugins/Google.Protobuf.dll`
2. `.dll.meta` 파일만 있고 `.dll` 파일이 없으면 **빌드 실패**

### 정책

- Google.Protobuf 기반 기능은 **필수 의존성**으로 취급한다
- `Google.Protobuf.dll`이 없거나 Unity가 dll을 찾지 못하면 `Devian.Core` 어셈블리가 컴파일에 실패할 수 있다
- 이 상태는 **허용되는 실패**이다 (편의성 우선 정책)

### 사용자 준비사항

| 항목 | 설명 |
|------|------|
| dll 파일 | `Google.Protobuf.dll`을 Unity 프로젝트에 포함 |
| 경로 | `Assets/Plugins/` 또는 Unity가 인식하는 위치 |
| 버전 | Google.Protobuf 3.x 이상 권장 |

> **참고:** `.dll.meta`만 커밋하고 실제 `.dll`을 .gitignore로 제외한 경우,
> 새로운 환경에서 clone 후 dll을 별도로 복사해야 한다.

---

## Reference

- Policy SSOT: `skills/devian/03-ssot/SKILL.md`
- DFF 규약: `skills/devian/31-class-cell-format/SKILL.md`
- 동작 정본: 런타임/제너레이터 코드
