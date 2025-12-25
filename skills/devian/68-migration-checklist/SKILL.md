# Devian – 68 Migration Checklist

## Purpose

**build.json v5 마이그레이션 및 빌드 검증을 위한 체크리스트를 제공한다.**

---

## Belongs To

**Tooling / Build**

---

## 마이그레이션 목표 (확정)

| 항목 | 규칙 |
|------|------|
| Build 단위 | Domain |
| 입력 탐색 | `{inputDirs.*Dir}/{domain}/...` 하위에서만 |
| Protocol | 폴더=domain, 파일명=namespace |
| 생성 위치 | `tempDir/{domain}/{cs\|ts\|data}` |
| outputs | copy 대상만 (primary 없음) |

---

## 폐기된 필드 (즉시 실패)

| 폐기 필드 | 대체 |
|----------|------|
| `inputs` | `inputDirs` |
| `targets` | ❌ (domain 내 `*TargetDirs`) |
| `variables` | ❌ |
| `output` | ❌ |
| `workspace.tempRoot` | `tempDir` |
| `protocolNamespaces` | `protocolFiles` |
| `contractsSpec` | `contractsFiles` |
| `tables.sources` | `tablesFiles` |

---

## 새 build.json 스펙

### 최상위 구조

```json
{
  "version": "5",
  "inputDirs": {
    "contractsDir": "input/contracts",
    "tablesDir": "input/tables",
    "protocolsDir": "input/protocols"
  },
  "tempDir": "temp/devian",
  "domains": { ... }
}
```

### Domain 구조

```json
{
  "contractsFiles": ["*.json"],
  "tablesFiles": ["*.xlsx"],
  "protocolFiles": ["C2Game.json", "Game2C.json"],
  "csTargetDirs": ["modules/cs/{domain}/Runtime/generated"],
  "tsTargetDirs": ["modules/ts/{domain}/generated"],
  "dataTargetDirs": ["modules/data/{domain}"]
}
```

---

## Validator 체크 항목 (빌드 초기 수행)

### 1. Config 구조 검증 (strict)

| 검증 | 설명 |
|------|------|
| 허용 키 외 존재 | 실패 |
| arrays가 array 타입 | 단일 string 금지 |
| 폐기 필드 사용 | 즉시 실패 |

### 2. 입력 파일 매칭 검증

```
각 domain에 대해:
- contractsFiles 패턴 매칭 결과가 0개면:
  → contractsFiles가 비어있지 않은데 0개면 실패
- tablesFiles 패턴 매칭 결과가 0개면:
  → tablesFiles가 비어있지 않은데 0개면 실패
- protocolFiles 패턴 매칭 결과가 0개면:
  → protocolFiles가 비어있지 않은데 0개면 실패
```

### 3. Protocol namespace 검증

| 검증 | 설명 |
|------|------|
| 파일명 기반 namespace | 강제 |
| JSON namespace 존재 시 | 파일명과 완전 일치해야 함 |
| namespace 충돌 | 같은 namespace 파일 2개 발견 시 실패 |

### 4. 산출물-로더 정합성 검증 (테이블)

```
Table.*.g.cs가 요구하는 키 목록과
data 출력 디렉토리에 실제 JSON 존재 검사
→ 누락되면 실패 (경고로 넘기지 말 것)
```

---

## Protocol 마이그레이션

### 파일명 = namespace 규칙

| Before | After |
|--------|-------|
| `ws/messages.json` | `ws/ws.json` |
| `net/messages.json` | `net/C2Game.json`, `net/Game2C.json` |

### JSON 내부 namespace 처리

| 상황 | 처리 |
|------|------|
| 필드 없음 (권장) | 파일명을 namespace로 사용 |
| 필드 있음 | 파일명과 일치 검증, 불일치 시 실패 |

### 구현 코드

```csharp
var filenameBase = Path.GetFileNameWithoutExtension(filePath);

if (spec.Namespace != null && spec.Namespace != filenameBase)
{
    throw new BuildException($"namespace mismatch: file={filenameBase}, json={spec.Namespace}");
}

spec.Namespace = filenameBase; // 강제 적용
```

---

## 생성/복사 규칙

### 생성 위치 (항상 tempDir)

```
{tempDir}/{domain}/cs/    ← C# 생성물
{tempDir}/{domain}/ts/    ← TS 생성물
{tempDir}/{domain}/data/  ← JSON 데이터
```

### 복사 규칙

```
source: {tempDir}/{domain}/{cs|ts|data}/
target: 각 {cs|ts|data}TargetDirs[]

primary 개념: ❌
from/to DSL: ❌
→ 항상 temp → targetDirs
```

---

## CLI 커맨드

```bash
devian build input/build/build.json

# 옵션
--domain <name>    도메인 단위 실행
--clean-temp       tempDir/{domain} 삭제 후 빌드
--clean-targets    copy 전에 targetDirs 삭제
```

---

## 마이그레이션 순서 (안전한 순서)

| 순서 | 작업 |
|------|------|
| 1 | 새 build.json 스펙 반영 + strict validator 추가 |
| 2 | 생성 output을 tempDir로 고정 |
| 3 | copy(targetDirs) 구현 |
| 4 | protocol 입력을 protocolFiles + 파일명=namespace로 변경 |
| 5 | 기존 IDL 파일명/구조 마이그레이션 |
| 6 | tables 입력 누락/데이터 누락 문제 해결 |
| 7 | `devian build`로 clean build 재현 가능 확인 |

---

## Acceptance Criteria

| # | 조건 |
|---|------|
| 1 | 폐기된 필드 사용 시 빌드 즉시 실패 |
| 2 | 입력 파일 매칭 0개 시 빌드 실패 (비어있지 않은 패턴인 경우) |
| 3 | Protocol namespace는 파일명에서 결정됨 |
| 4 | Table loader 요구 키와 data 파일 정합성 검증됨 |
| 5 | clean build 재현 가능 |

---

## Related Skills

| Skill | 관계 |
|-------|------|
| `60-build-pipeline` | 빌드 스펙 |
| `62-protocolgen-implementation` | Protocol 생성 |
| `63-build-runner` | 빌드 실행 |

---

## Version History

| Version | Date | Changes |
|---------|------|---------|
| 0.1.0 | 2024-12-25 | Initial migration checklist |
