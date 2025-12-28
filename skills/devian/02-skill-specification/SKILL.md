# Devian – 02 Skill Specification

## 1. Skill의 정의

**Skill은 Devian Framework의 확장 단위다.**

Skill은:
- 플러그인이 **아니다**
- 선택적 도구가 **아니다**
- 외부 애드온이 **아니다**

**Skill은 Devian Framework를 구성하는 정식 구성 요소다.**

---

## 1.5. IR (Intermediate Representation) 정책

**Devian은 스키마 기반 중립 표현(IR)을 사용한다.**

### SSOT (Single Source of Truth)

| 구분 | 값 | 설명 |
|------|-----|------|
| **SSOT** | Excel | Table 스키마의 유일한 진실 원천 |
| **contracts** | JSON | 타입 정의 (*.json) |
| **tables** | Excel | 테이블 데이터 (*.xlsx) |

> ⚠️ Table 스키마의 SSOT는 Excel이다.

---

## 1.6. Domain Root (정본)

**Devian에서 Domain은 디렉터리 이름이 아니라 논리 단위이다.**

모든 Domain의 실제 루트 경로는 다음으로 고정된다:

```
input/<Domain>/
```

`domains/` 디렉터리는 Devian 구조에 존재하지 않는다.
`{Domain}` 표기는 문서/설명용 플레이스홀더이며,
실제 파일 시스템 경로를 의미하지 않는다.

---

## 1.7. 도메인 폴더 규약 (v9)

**수동 작성 proto 폴더(proto-manual)는 폐기되었다.**

### Data Domain 폴더 구조 (Common 예시)

```
input/Common/
├── contracts/     # 계약 정의 (*.json)
└── tables/        # 테이블 정의 (*.xlsx)
```

### Protocol-only Domain 폴더 구조 (C2Game 예시)

```
input/C2Game/
└── protocols/
    └── C2Game.proto   # 단일 파일
```

### 소유권 규칙 (MUST)

| 폴더 | 소유권 | 규칙 |
|------|--------|------|
| `contracts/*.json` | 사람 | 계약 정의 |
| `tables/*.xlsx` | 사람 | 테이블 정의 |
| `protocols/*.proto` | 사람 | Protocol IDL (단일 파일) |

---

## 1.8. Common 도메인 및 Devian.Common 모듈

> Common 관련 규약/구현 지침은 `skills/devian-common/` 스킬을 참조한다.

---

## 1.10. 기계 소유 폴더 (proto-gen/)

각 domain은 기계 생성물 전용 폴더(`proto-gen/`)를 가진다. IR(Protobuf) 스키마(.proto)와 protoc 생성 코드는 `proto-gen/` 아래에만 존재하며, 사람이 직접 수정하지 않는다.

```
input/<Domain>/proto-gen/
├── schema/     # Excel에서 생성된 .proto (기계 생성, 수정 금지)
├── manifest/   # Tag Registry 파일 (기계 관리, 커밋 필수)
└── cs/         # protoc로 생성된 C# code (기계 생성, 수정 금지)
```

### Tag Registry 정책

**field number(tag)는 Tag Registry로 자동 관리한다.**

#### Registry 파일 위치

```
input/<Domain>/proto-gen/manifest/<TableName>.tags.json
```

#### Registry 파일 형식

```json
{
  "version": 1,
  "fields": {
    "Id": 1,
    "Name": 2,
    "Cost": 3
  },
  "reserved_tags": [4, 7],
  "reserved_names": ["OldFieldName"]
}
```

#### Tag 관리 규칙 (MUST)

| # | 규칙 |
|---|------|
| 1 | tag는 **단조 증가** 발급 |
| 2 | 발급된 tag는 **절대 변경/재사용 금지** |
| 3 | 삭제 필드는 **reserved**로 남긴다 |
| 4 | rename은 **tag 유지** |

#### Add/Rename/Delete 규칙

| 작업 | 규칙 |
|------|------|
| **Add** | 새 필드 등장 → 새 tag 발급 |
| **Rename** | 이름 변경해도 tag 유지, Registry 키 이름만 업데이트 |
| **Delete** | proto에 `reserved <tag>;` 및 `reserved "<name>";` 생성, Registry에도 기록, tag 재사용 금지 |

### proto-gen 커밋 정책

Protobuf IR artifacts (.proto and generated code) are committed to the repository under each domain's `proto-gen/` directory to ensure reproducibility and Unity stability.

| 정책 | 값 |
|------|-----|
| Git 커밋 | ✅ proto-gen/** is committed |
| .gitignore | ❌ proto-gen 제외 금지 |
| 빌드 시 재생성 | ❌ 자동 재생성 안 함 |
| 생성 시점 | 명시적 도구 실행 시에만 |

> Proto generation is an explicit developer action, not a build step.

### IR 포맷

| 구분 | 포맷 | 역할 |
|------|------|------|
| IR (canonical) | Protobuf | 유일한 canonical representation |
| 보조 표현 | JSON | 디버깅, 검증, 외부 노출 |

IR의 유일한 canonical representation은 Protobuf이다. JSON은 IR 상태를 기반으로 한 보조 표현이며, 결과 동일성의 기준이 아니다.

---

## 1.9. Excel→Proto 강검증 원칙

**생성기는 Excel을 읽고 .proto를 생성하기 전에 강한 검증을 수행한다.**

> **"생성 실패가 정상이다"** — 검증 위반 시 .proto 생성 자체를 실패 처리한다.

### 검증 체크리스트

| # | 검증 항목 | 설명 |
|---|----------|------|
| 1 | Header 4줄 고정 | Row1/Row2/Row3/Row4 의미 위반 검사 |
| 2 | Type 문법 (Row2) | 허용 타입만 사용, ref: / ref:Common. 포맷 검증 |
| 3 | Options 문법 (Row3) | 알 수 없는 옵션 키/값 에러, 타입 정보 삽입 금지 |
| 4 | ref 대상 존재 | 스캔 범위 내 정의 0개/중복 시 실패 |
| 5 | Data 행 타입/범위 (Row5+) | 파싱 실패, 범위 초과 에러 |

> **핵심:** Excel 데이터가 가장 많이 깨지므로, 여기서 최대한 막는다.

---

## 1.10. Excel Type 규칙 (Row 2)

Excel Row 2에서 허용되는 타입은 다음과 같다. (상세: `24-table-authoring-rules`)

### Scalar 타입

| 타입 | 범위 | Proto 매핑 |
|------|------|-----------|
| `byte` | -128 ~ 127 | int32 |
| `ubyte` | 0 ~ 255 | uint32 |
| `short` | -32768 ~ 32767 | int32 |
| `ushort` | 0 ~ 65535 | uint32 |
| `int` | 32비트 | int32 |
| `uint` | 32비트 | uint32 |
| `long` | 64비트 | int64 |
| `ulong` | 64비트 | uint64 |
| `float` | | float |
| `string` | | string |

### 참조 타입

| 타입 | 설명 |
|------|------|
| `ref:{Name}` | 같은 도메인 내 proto 정의 참조 |
| `ref:Common.{Name}` | Common 도메인 proto 정의 참조 |

> **중요:**
> - enum / message 구분은 **proto 정의 기준**이며, Excel 규칙이 아님
> - Common 외 cross-domain 참조는 **1단계에서 금지**

### 배열 타입

모든 Scalar 및 ref 타입에 대해 `[]` 접미사로 배열 허용.

### ref 스캔 범위 (v9)

**수동 작성 proto 폴더(proto-manual)는 폐기되었다.**

| ref 형식 | 스캔 범위 |
|----------|----------|
| `ref:{Name}` | `<D>/contracts/*.json` |
| `ref:Common.{Name}` | `Common/contracts/*.json` |

### ref 검증 규칙 (MUST)

| # | 규칙 |
|---|------|
| 1 | `{Name}`은 enum 또는 message로 정의되어야 함 |
| 2 | 정의가 **0개** → 생성 실패 |
| 3 | 정의가 **2개 이상** (중복) → 생성 실패 |
| 4 | Excel은 enum/message 구분 안 함, **proto 정의 기준** |

### 범위 검증 정책

> **중요:**
> - `byte`, `ubyte`, `short`, `ushort`의 의미적 범위는 **Devian 규약**이다.
> - **Protobuf는 이 범위를 강제하지 않는다.**
> - 범위 검증 책임은 **Devian Generator/Loader**에 있다.
> - 범위 초과 시 **Load 실패** (silent clamp 금지, 암묵적 변환 금지)

---

## 1.11. Method Naming Policy (Devian)

Devian 프로젝트의 모든 C# 코드는 아래 메소드 네이밍 규칙을 따른다.

| 접근자 | 네이밍 규칙 | 예시 |
|--------|------------|------|
| public 외부 API | `MethodName` | `public void LoadData()` |
| internal 또는 내부 전용 public | `_MethodName` | `public void _LoadProto()` |
| private / protected | `methodName` | `private void parseData()` |

### 내부 전용 API 규칙 (MUST)

| # | 규칙 |
|---|------|
| 1 | `_` 접두사는 **내부 전용 API**임을 의미한다 |
| 2 | 외부 비즈니스 코드에서의 직접 호출은 **금지** |
| 3 | 프레임워크/생성기/로더/테스트 전용으로만 사용 |
| 4 | `_` 접두사 메소드는 **PascalCase** 유지 |

### private / protected 규칙 (MUST)

| # | 규칙 |
|---|------|
| 1 | **소문자**로 시작 |
| 2 | **camelCase** 사용 |
| 3 | `_` 접두사 **금지** |

---

## 2. Target Type 정의

Devian에서 `*TargetDirs`는 생성 파일이 떨어지는 위치가 아니라 **모듈 루트**를 의미하며, 생성 파일의 실제 위치는 Target Type별로 고정된 하위 디렉터리 규칙을 따른다.

### Target Type 목록

| Target | 모듈 형태 | 책임 |
|--------|----------|------|
| `cs` | C# Class Library | .NET Standard 2.1 클래스 라이브러리 모듈 |
| `ts` | NodeJS 라이브러리 | npm 패키지 형태의 TypeScript 모듈 |
| `upm` | Unity Package | Unity Package Manager 컨테이너 |
| `data` | 데이터 컨테이너 | 보조 표현(JSON) 산출 컨테이너 |

### Target Type별 생성 경로

| Target | 모듈 루트 | 생성 파일 위치 |
|--------|----------|---------------|
| `cs` | `{csTargetDir}` | `{csTargetDir}/generated/**` |
| `ts` | `{tsTargetDir}` | `{tsTargetDir}/generated/**` |
| `upm` | `{upmTargetDir}` | `{upmTargetDir}/Runtime/**` |
| `data` | `{dataTargetDir}` | `{dataTargetDir}/json/**` |

### Table API 생성 구조

Table API 생성물은 `namespace Devian.Tables` 하위에 `Table.TB_{TableName}` 정적 타입으로 생성된다.

#### Entity / Table / Converter 관계

Entity는 순수 데이터 모델이다. Entity는 직렬화 방식을 알지 않는다.

Table 컨테이너는 Entity 컬렉션을 관리한다.

Converter는 IR(Protobuf) ↔ Entity **바이너리 변환**을 담당한다.

> **JSON I/O 정본 경로 (SSOT)**
>
> JSON Load/Save는 IEntityConverter를 통하지 않는다. 정본 경로는 `28-json-row-io` 스킬에 정의:
> - **Load**: general JSON(NDJSON) → Descriptor-driven IMessage build → `entity._LoadProto()`
> - **Save**: `entity._SaveProto()` → proto→general JSON 매핑 규칙 → NDJSON string
>
> `IEntityConverter.ToJson/FromJson`은 테이블 정식 I/O 경로에서 사용하지 않는다.

```csharp
namespace Devian.Tables
{
    public static partial class Table
    {
        public static class TB_TestSheet : ITableContainer
        {
            private static readonly Dictionary<int, TestSheetRow> _cache = new();
            
            public static bool IsLoaded => _cache.Count > 0;
            
            // ITableContainer implementation
            public void Clear();
            public void LoadFromJson(string json, LoadMode mode = LoadMode.Merge);
            public string SaveToJson();
            public void LoadFromBase64(string base64, LoadMode mode = LoadMode.Merge);
            public string SaveToBase64();
            
            // Lookup
            public static TestSheetRow Get(int key);
            public static bool TryGet(int key, out TestSheetRow? row);
        }
    }
}
```

### Row 타입 인터페이스 규칙

Row 타입은 `Devian.Core.IEntity`를 구현한다.

PK가 있는 Row는 `Devian.Core.IEntityWithKey<PKType>`를 추가로 구현하며 `GetKey()`를 제공한다.

### IEntityConverter 인터페이스

Converter는 IR(Protobuf) ↔ Entity **바이너리 변환**을 담당한다.

**JSON I/O는 IEntityConverter를 통하지 않는다.** `28-json-row-io` 파이프라인 참조.

```csharp
namespace Devian.Core
{
    public interface IEntityConverter<TEntity>
    {
        // IR 경로 (Protobuf Binary) - 정식 경로
        byte[] ToBinary(IReadOnlyList<TEntity> entities);
        IReadOnlyList<TEntity> FromBinary(byte[] bytes);
        
        // [NOT USED IN TABLE I/O] - Legacy/Debug only
        // JSON I/O는 28-json-row-io 정본 파이프라인 사용
    }
}
```

### LoadMode enum

LoadMode는 데이터 병합/교체 동작을 제어한다.

```csharp
namespace Devian.Core
{
    public enum LoadMode
    {
        Merge,      // 기존 캐시 유지 + 새 데이터 병합
        Replace     // 기존 캐시 Clear 후 로드
    }
}
```

---

## 3. Skill의 역할

Skill은 다음 중 하나 이상을 수행한다:

| 역할 | 설명 |
|------|------|
| 코드 생성 | 특정 런타임을 위한 코드 생성 |
| 데이터 변환 | 특정 플랫폼을 위한 데이터 변환 |
| 규약 제공 | 특정 스택(NestJS, Unity, C# 등)에 대한 규약 |
| 런타임 연결 | 빌드 결과를 실제 실행 환경에 연결 |

---

## 4. 공식 Skill 범주

다음은 Devian Framework의 공식 Skill 범주다:

### Server Skills

| Skill | 설명 |
|-------|------|
| NestJS Server Skill | API, 네트워크, 서버 런타임 연결 |
| Express Server Skill | 경량 서버 런타임 |

### Client Skills

| Skill | 설명 |
|-------|------|
| C# Network Client Skill | 메시지/프로토콜 소비 |
| TypeScript Client Skill | 웹 클라이언트 |

### Engine Skills

| Skill | 설명 |
|-------|------|
| Unity Skill | 게임 엔진 런타임 연결 |
| Godot Skill | Godot 엔진 연결 |

### Tooling Skills

| Skill | 설명 |
|-------|------|
| Validation Skill | 데이터 검증 |
| Test Skill | 자동화 테스트 |
| Visualization Skill | 데이터 시각화 |

이 목록은 확장 가능하지만, **모든 확장은 반드시 Skill로 정의된다.**

---

## 5. Skill 설계 원칙

| # | 원칙 |
|---|------|
| 1 | Skill은 Framework 규약을 **변경하지 않는다** |
| 2 | Skill은 정의 포맷을 **독점하지 않는다** |
| 3 | Skill은 단일 빌드 흐름에 **종속된다** |
| 4 | Skill은 독립적으로 **추가·제거 가능**해야 한다 |

---

## 6. Framework vs Skill 경계

| 항목 | Framework | Skill |
|------|:---------:|:-----:|
| 정의 포맷 | ✔ | ✖ |
| 빌드 규약 | ✔ | ✖ |
| 코드 생성 규칙 | ✔ | ✖ |
| 런타임 연결 | ✖ | ✔ |
| 플랫폼 의존성 | ✖ | ✔ |
| 네트워크 스택 | ✖ | ✔ |
| 엔진 로직 | ✖ | ✔ |

---

## 7. Skill 디렉토리 구조

```
skills/
├── devian/              ← Framework 핵심 스킬 (규약)
│   ├── 00-rules-minimal/
│   ├── 01-devian-core-philosophy/
│   ├── 02-skill-specification/
│   └── ...
│
├── server/              ← Server Skills
│   ├── nestjs/
│   └── express/
│
├── client/              ← Client Skills
│   ├── csharp-network/
│   └── typescript/
│
├── engine/              ← Engine Skills
│   ├── unity/
│   └── godot/
│
└── tooling/             ← Tooling Skills
    ├── validation/
    └── test/
```

---

## 8. Skill 문서 구조

모든 Skill 문서는 다음 구조를 따른다:

```markdown
# {Category} – {Skill Name}

## Purpose
## Scope (In/Out)
## Hard Rules (MUST)
## Soft Rules (SHOULD)
## Inputs / Outputs
## Integration Points
## Related Skills
## Version History

| Version | Date | Changes |
|---------|------|---------|
| 1.0.0 | 2025-12-28 | Initial |
## Hard Rules (MUST)

| # | Rule |
|---|------|
| 1 | 모든 확장은 **Skill**로 정의한다 |
| 2 | Skill은 Framework 규약을 **변경하지 않는다** |
| 3 | Skill은 단일 빌드 흐름에 **종속된다** |
| 4 | NestJS, Unity 관련 기능은 **Skill**이다 |

---

## Soft Rules (SHOULD)

| # | Rule |
|---|------|
| 1 | Skill은 명확한 범주(Server/Client/Engine/Tooling)에 속해야 한다 |
| 2 | Skill 간 의존성은 최소화한다 |
| 3 | Skill은 자체 테스트를 포함해야 한다 |

---

## Related Skills

| Skill | 관계 |
|-------|------|
| `01-devian-core-philosophy` | Framework 철학 |
| `00-rules-minimal` | Hard Rules |
| `60-build-pipeline` | 빌드 규약 |
| `28-json-row-io` | JSON I/O 정본 파이프라인 |

---

## Version History

| Version | Date | Changes |
|---------|------|---------|
| 1.0.0 | 2025-12-28 | Initial |
