# Devian – 10 Core Runtime

## Purpose

**Core Runtime은 Devian Framework의 공유 기반 계층이다.**

Core Runtime은 **공유 프리미티브와 확장 지점**을 정의한다.
앱 구조, 전송 방식, 실행 정책은 **Skill의 책임**이다.

Core Runtime은 다음을 만족한다:

- OS(macOS / Windows)와 무관
- 실행 환경(Unity / Server / Tool)과 무관
- 저장 방식(File / Memory / DB)과 무관
- 전송 방식(HTTPS / Socket / IPC)과 무관

**Entity가 어디서 오고 어디로 가든 동일하게 다뤄질 수 있는 기반 계층**을 제공한다.

---

## Scope

### In Scope

| 항목 | 설명 |
|------|------|
| 공통 타입 | `CoreResult`, `CoreError`, `ParseResult` 등 |
| 키/식별자 | `IEntity`, `PacketEnvelope` 등 |
| 레지스트리 개념 | `CoreValueParserRegistry` |
| 파싱/해석 hook | `IValueParser`, `ParseContext` |

### Out of Scope (Skill 영역)

| 항목 | 담당 Skill |
|------|-----------|
| IO 방식 | File / Memory / DB → 각 플랫폼 Skill |
| Transport | HTTP, WebSocket, MQ → Network Skill |
| Serialization policy | Protobuf → Serializer Skill (JSON은 optional view) |
| 서버 연동 | NestJS 등 → Server Skill |
| generated 코드 의존 | Core는 generated를 직접 참조하지 않음 |
| contracts 구조 해석 | Core는 contracts 경로/구조를 알지 않음 |

---

## Hard Rules (MUST)

| # | Rule |
|---|------|
| 1 | Core runtime은 generated 코드를 직접 참조하지 않는다 |
| 2 | Core runtime은 contracts의 구조를 해석하지 않는다 |
| 3 | Core runtime은 확장 포인트만 제공한다 (구현 강제 안 함) |
| 4 | Devian.Core는 IR의 구체적 포맷(Protobuf)에 대해 알지 않는다 |
| 5 | Devian.Core는 Google.Protobuf 패키지를 참조하지 않는다 |

> IR은 Serializer Skill을 통해서만 다뤄진다.
> `IProtoEntity<TProto>`는 `Devian.Protobuf`에 정의된다.
> 런타임 Protobuf 변환기는 `Devian.Protobuf`에, 생성기는 `Devian.Tools`에 있다.

---

## Soft Rules (SHOULD)

| # | Rule |
|---|------|
| 1 | 데이터는 가능한 한 string / raw 형태로 취급 |
| 2 | 구체 타입 변환은 **Skill** 책임 |
| 3 | 불변(immutable) 타입을 선호한다 |

> ⚠️ "항상 string으로 로드" 같은 절대 규칙은 두지 않는다. 이후 binary/raw 형태가 들어올 수 있다.

---

## Responsibilities

1. **추상 타입 제공** — Result, Error, Entity 등
2. **확장 가능한 registry 제공** — ValueParser 등록/조회
3. **파싱/해석 hook 제공** — IValueParser, ParseContext

**그 외 책임은 모두 Out of Scope다.**

---

## Core vs Common 경계

| Devian.Common | Devian.Core |
|---------------|-------------|
| 데이터 그 자체 | 규칙과 흐름의 추상화 |
| 값 타입, enum, 순수 함수 | 파서, 레지스트리, 핸들러 인터페이스 |
| 상태 없음 / 로직 거의 없음 | Common 타입을 사용함 |
| `GFloat2`, `GColor32`, `GEntityId` | `CoreResult`, `CoreError`, `IPacketHandler` |

**Core는 Common에 의존한다 (반대 금지).**

---

## Provided Types (Summary)

| 파일 | 타입 | 설명 |
|-----|------|------|
| `CoreResult.cs` | `CoreResult`, `CoreResult<T>` | 성공/실패 결과 타입 |
| `CoreError.cs` | `CoreError` | 에러 정보 (코드, 메시지, 체인) |
| `IEntityConverter.cs` | `IEntityConverter<TEntity>` | 테이블 Entity 직렬화 계약 (정식) |
| `LoadMode.cs` | `LoadMode` | 테이블 로드 모드 (Merge, Replace) |
| `ParseResult.cs` | `ParseResult`, `ParseResult<T>` | 파싱 결과 타입 |
| `ParseContext.cs` | `ColumnSchema`, `RowContext`, `ParseContext` | 파싱 컨텍스트 |
| `IValueParser.cs` | `IValueParser`, `IValueParser<T>`, `ValueParserBase<T>` | 값 파서 인터페이스 |
| `CoreValueParserRegistry.cs` | `CoreValueParserRegistry` | 파서 레지스트리 |
| `PacketEnvelope.cs` | `PacketEnvelope`, `PacketEnvelopeBuilder` | 네트워크 패킷 |
| `IPacketHandler.cs` | `IPacketHandler`, `PacketHandlerBase<T>`, `RequestHandlerBase<TReq,TRes>` | 패킷 핸들러 |
| `IEntity.cs` | `IEntity`, `IEntityWithKey<TKey>` | 엔티티 인터페이스 |
| `ITableContainer.cs` | `ITableContainer` | 테이블 컨테이너 인터페이스 |

---

## Directory & Project Structure

### Domain Root (정본)

**Devian에서 Domain은 디렉터리 이름이 아니라 논리 단위이다.**

모든 Domain의 실제 루트 경로는 다음으로 고정된다:

```
input/<Domain>/
```

별도의 도메인 루트 디렉터리는 Devian 구조에 없다.  
`{Domain}` 표기는 문서/설명용 플레이스홀더이며, 실제 파일 시스템 경로를 의미하지 않는다.

### Framework vs Modules 역할 분리

| 디렉토리 | 역할 | 내용 |
|----------|------|------|
| `framework/` | 언어별 Core 라이브러리 | Devian Framework 핵심 코드 |
| `modules/` | 도메인 산출물 | Devian 빌드가 생성하는 코드/데이터 |
| `input/` | 도메인 입력 | 스키마, 테이블, 프로토콜 정의 |

> ⚠️ `packages/` 용어는 사용하지 않는다 (Unity `Packages/`와 혼동 방지)

### 기계 소유 폴더 (proto-gen/)

기계 생성물은 `input/<Domain>/proto-gen/` 아래에 배치된다. 이 폴더는 기계 생성물 전용이며 사람이 수정하지 않는다.

```
input/<Domain>/proto-gen/
├── schema/     # .proto 파일 (Excel → 생성)
└── cs/         # C# 코드 (protoc → 생성)
```

### Serialization / Build Policy

`proto-gen/` outputs are pre-generated and committed. Build and runtime must not depend on protoc availability.

| 정책 | 설명 |
|------|------|
| 커밋 | proto-gen/** is committed to repository |
| 빌드 의존성 | protoc 설치 불필요 |
| 런타임 의존성 | proto-gen 재생성 불필요 |

### Unity UPM 패키지 구조 (modules/upm)

`modules/upm/{Domain}/`은 **Unity UPM 패키지 루트**다.

| 파일 | 설명 |
|------|------|
| `package.json` | UPM 패키지 매니페스트 |
| `README.md` | 패키지 설명 |
| `Runtime/{AssemblyName}.asmdef` | Runtime 어셈블리 정의 |
| `Runtime/generated/*.g.cs` | 자동 생성 코드 (수정 금지) |
| `Editor/{AssemblyName}.Editor.asmdef` | Editor 어셈블리 정의 |

> ⚠️ `Runtime/generated/` 내 `.g.cs` 파일은 Devian.Tools가 생성한다. 수동 편집 금지.

### 언어별 Core 모듈 위치

| 언어 | 경로 | 설명 |
|------|------|------|
| C# | `framework/cs/Devian.Core/` | .NET Standard 2.1 라이브러리 |
| TypeScript | `framework/ts/devian-core/` | npm 패키지 |

### C# / TS Core 대칭 정책

- Devian Framework는 **C# / TS Core를 대칭적으로 제공**한다
- 동일한 인터페이스와 타입을 양 언어에서 사용 가능
- TS Core는 **Implement Skill**을 통해 생성·갱신될 수 있다

### C# Core 구조

```
devian/
├── framework/
│   ├── cs/
│   │   ├── Devian.Common/       ← Core가 참조
│   │   └── Devian.Core/
│   │       ├── Devian.Core.csproj
│   │       └── src/
│   │           ├── CoreResult.cs
│   │           ├── CoreError.cs
│   │           ├── IEntityConverter.cs
│   │           ├── LoadMode.cs
│   │           ├── ParseResult.cs
│   │           ├── ParseContext.cs
│   │           ├── IValueParser.cs
│   │           ├── CoreValueParserRegistry.cs
│   │           ├── PacketEnvelope.cs
│   │           ├── IPacketHandler.cs
│   │           ├── IEntity.cs
│   │           └── ITableContainer.cs
│   └── ts/
│       └── devian-core/         ← TS Core (대칭)
│           ├── package.json
│           └── src/
│               └── index.ts
├── modules/                      ← 빌드 산출물
│   ├── cs/{Domain}/generated/    ← C# 라이브러리 모듈
│   ├── upm/{Domain}/Runtime/     ← Unity UPM 패키지
│   ├── ts/{Domain}/generated/
│   └── data/{Domain}/
└── Devian.sln
```

### Namespace Convention

```
Devian.Core    ← 단일 네임스페이스
```

**중요: 하위 네임스페이스를 만들지 않는다.**

---

## Target & Compatibility

### Target: netstandard2.1 Only

```xml
<TargetFramework>netstandard2.1</TargetFramework>
```

| 환경 | 호환성 | 비고 |
|------|--------|------|
| Unity 2021+ | ✅ | .NET Standard 2.1 지원 |
| .NET 5/6/7/8/9 | ✅ | netstandard2.1 호환 |
| .NET Core 3.x | ✅ | netstandard2.1 호환 |

### Unity Compatibility

- **C# 9.0 제약** — C# 10.0+ 기능 사용 금지
- **블록 네임스페이스 사용** (`namespace X { }`)
- **ImplicitUsings 금지** — 명시적 using 문 사용
- **IsExternalInit** — Common에만 위치

---

## Implementation Rules

### DO (해야 할 것)

1. `netstandard2.1` 호환 유지
2. 블록 네임스페이스 사용 (`namespace X { }`)
3. 모든 파일 상단에 `#nullable enable` 추가
4. 모든 파일에 명시적 using 문 추가
5. 모든 public 타입에 XML 문서 주석 작성
6. 네임스페이스는 `Devian.Core` 단일 레벨만 사용
7. Devian.Common을 참조하여 G* 타입 사용

### DON'T (하지 말 것)

1. **파일 범위 네임스페이스 사용 금지** (`namespace X;` ❌ → `namespace X { }` ✅)
   - 모든 수동 코드(.cs)에 적용
   - 모든 코드 생성기(Generators/*.cs)에 적용
   - 모든 생성 코드(.g.cs)에 적용
   - 이유: netstandard2.1은 기본 C# 9.0, 파일 범위 네임스페이스는 C# 10+ 전용
2. ImplicitUsings 사용 금지
3. IsExternalInit.cs 추가 금지 (Common에만 있음)
4. 외부 라이브러리 직접 참조 금지
5. Unity 전용 API 사용 금지
6. 구체적인 I/O 구현 금지
7. 하위 네임스페이스 생성 금지
8. 도메인 메시지/콘텐츠 타입 정의 금지

---

## csproj Template

경로: `framework/cs/Devian.Core/Devian.Core.csproj`

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>netstandard2.1</TargetFramework>
    <RootNamespace>Devian.Core</RootNamespace>
  </PropertyGroup>
  <ItemGroup>
    <ProjectReference Include="..\Devian.Common\Devian.Common.csproj" />
  </ItemGroup>
</Project>
```

---

## Acceptance Criteria

| # | 조건 |
|---|------|
| 1 | Core runtime이 "중앙 컨트롤러"처럼 보이지 않는다 |
| 2 | generated / server / transport 단어가 **책임 영역**에 포함되지 않는다 |
| 3 | 이후 스킬(21, 26, 51)이 이 문서를 전제로 설명 가능하다 |
| 4 | Out of Scope가 명시적으로 정의되어 있다 |

---

## Related Skills

| Skill | 관계 |
|-------|------|
| `01-devian-core-philosophy` | **기준** — Framework 철학 |
| `02-skill-specification` | **확장** — Skill 경계 정의 |
| `12-common-standard-types` | **의존** — Common 타입 참조 |
| `11-core-serializer-protobuf` | **확장** — Protobuf 기반 직렬화 |
| `13-devian-core-ts-generator` | **구현** — TS Core 생성기 |
| `00-rules-minimal` | **규칙** — 네임스페이스 명명 규칙 |

---

## Version History

| Version | Date | Changes |
|---------|------|---------|
| 1.0.0 | 2025-12-28 | Initial |
