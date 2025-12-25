# Devian – 70 Common Domain Default Policy

## Purpose

**Devian 프로젝트는 항상 `common` 도메인을 기본으로 포함한다.**

---

## Belongs To

**Core / Build**

---

## 1. common 도메인 필수 규칙

- `common` 도메인은 Devian 프로젝트에 **항상 존재해야 한다**
- `common` 도메인은:
  - 공통 contracts
  - 공통 table seed / 기본 데이터
  - 공통 타입 및 검증 기준
  을 제공한다
- CI 및 로컬 기본 테스트는 **항상 `common` 도메인 빌드 기준**으로 수행한다

---

## 2. 입력 디렉토리 구조

```
input/
├── common/
│   ├── contracts/     ← 공통 타입
│   │   ├── types.json
│   │   └── TestContract.json
│   ├── tables/        ← 공통 테이블
│   │   └── TestTable.xlsx
│   └── protocols/     ← (비어있음)
├── game/
│   ├── contracts/     ← game 전용 타입
│   ├── tables/        ← game 전용 (common override 가능)
│   └── protocols/     ← Protocol은 병합 없음
│       ├── C2S.json
│       └── S2C.json
└── build/
    └── build.json
```

---

## 3. dependsOnCommon 규칙

- 모든 domain은 기본적으로 `dependsOnCommon = true`
- domain 설정에 명시되지 않으면 **true**
- `common` 도메인 자신에게는 이 옵션을 적용하지 않는다

---

## 4. dependsOnCommon = true 의 의미

`dependsOnCommon = true` 인 domain `X`는 빌드 시:

| 유형 | 입력 집합 |
|------|----------|
| Contracts | input/common/contracts + input/X/contracts |
| Tables | input/common/tables + input/X/tables |
| Protocols | input/X/protocols 만 |

---

## 5. 충돌 처리 규칙

| 유형 | 규칙 |
|------|------|
| contracts 충돌 | 동일 파일/타입 충돌 시 **빌드 실패** |
| tables 충돌 | domain(X)이 common을 override |
| protocol | common과 병합하지 않으므로 충돌 없음 |

---

## 6. Hard Rules (MUST)

| # | 규칙 |
|---|------|
| 1 | common 도메인은 항상 존재해야 한다 |
| 2 | 모든 domain은 기본적으로 dependsOnCommon = true |
| 3 | CI/테스트는 common 도메인 빌드 기준 |
| 4 | contracts 충돌 시 빌드 실패 |
| 5 | tables 충돌 시 domain이 common을 override |

---

## 7. 절대 금지 사항 (MUST NOT)

| # | 금지 |
|---|------|
| 1 | common 없이 domain을 설계하거나 생성 |
| 2 | common을 선택 사항(optional)으로 취급 |
| 3 | common을 runtime 의존성 집합으로 사용 |
| 4 | common이 다른 domain의 산출물을 import |

---

## 8. build.json 예시

```json
{
  "version": "5",
  "inputDir": "input",
  "tempDir": "temp/devian",
  
  "domains": {
    "common": {
      "csTargetDirs": ["modules/cs/common/Runtime/generated"],
      "tsTargetDirs": ["modules/ts/common/generated"],
      "dataTargetDirs": ["modules/data/common"]
    },
    "game": {
      "dependsOnCommon": true,
      "csTargetDirs": ["modules/cs/game/Runtime/generated"],
      "tsTargetDirs": ["modules/ts/game/generated"],
      "dataTargetDirs": ["modules/data/game"]
    }
  }
}
```

---

## Related Skills

| Skill | 관계 |
|-------|------|
| `60-build-pipeline` | 빌드 스펙 |
| `63-build-runner` | 빌드 실행 |

---

## Version History

| Version | Date | Changes |
|---------|------|---------|
| 0.2.0 | 2024-12-25 | 입력 폴더 구조 변경 반영 |
| 0.1.0 | 2024-12-25 | Initial |

---

## 한 줄 요약

**Devian의 모든 domain은 기본적으로 common을 포함하며, common은 개발과 테스트의 기준점이다.**
