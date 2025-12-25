# Devian – 60 Build Pipeline

## Purpose

**Devian 빌드 파이프라인 스펙을 정의한다.**

---

## Belongs To

**Build / Core**

---

## 1. 입력 폴더 규칙 (v5)

입력 디렉토리 구조:

```
input/
├── {domain}/
│   ├── contracts/    ← *.json
│   ├── tables/       ← *.xlsx
│   └── protocols/    ← *.json
└── build/
    └── build.json
```

예시:

```
input/
├── common/
│   ├── contracts/
│   │   ├── types.json
│   │   └── TestContract.json
│   ├── tables/
│   │   └── TestTable.xlsx
│   └── protocols/
├── ws/
│   ├── contracts/
│   ├── tables/
│   └── protocols/
│       └── ws.json
└── build/
    └── build.json
```

---

## 2. build.json 스펙 (v5)

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
    "ws": {
      "dependsOnCommon": true,
      "csTargetDirs": ["modules/cs/ws/Runtime/generated"],
      "tsTargetDirs": ["modules/ts/ws/generated"],
      "dataTargetDirs": []
    }
  }
}
```

---

## 3. 빌드 실행 정책

`devian build` 실행 시:

1. `domains`를 순회
2. 각 domain에서:
   - `{inputDir}/{domain}/contracts` 존재하면 → contracts 생성
   - `{inputDir}/{domain}/tables` 존재하면 → tables 생성
   - `{inputDir}/{domain}/protocols` 존재하면 → protocols 생성
3. 생성은 항상 `{tempDir}/{domain}/{cs|ts|data}`
4. 생성 후 `{type}TargetDirs[]`로 copy

---

## 4. dependsOnCommon 정책

`dependsOnCommon: true` (기본값) 인 domain은:

| 유형 | 병합 |
|------|------|
| contracts | common + domain |
| tables | common + domain (domain이 override) |
| protocols | domain만 (common 병합 안 함) |

---

## 5. Protocol namespace 정책

- namespace = 파일명 (확장자 제외)
- JSON 내부 `namespace`는 검증만 (생성 기준으로 사용 금지)

예: `ws.json` → namespace는 `ws`

---

## 6. 폐기된 필드

| 필드 | 상태 |
|------|------|
| `inputDirs` | 폐기 |
| `contractsFiles` | 폐기 |
| `tablesFiles` | 폐기 |
| `protocolFiles` | 폐기 |
| `protocolNamespaces` | 폐기 |

---

## Hard Rules (MUST)

| # | 규칙 |
|---|------|
| 1 | 입력 경로는 `input/{domain}/contracts\|tables\|protocols` |
| 2 | 생성은 항상 tempDir → targetDirs copy |
| 3 | Protocol namespace = 파일명 |
| 4 | 폐기된 필드 사용 시 빌드 실패 |

---

## 절대 금지 사항 (MUST NOT)

| # | 금지 |
|---|------|
| 1 | `input/contracts/{domain}` 형태 사용 |
| 2 | `contractsFiles`, `tablesFiles` 등 폐기 필드 사용 |
| 3 | Protocol JSON 내부 namespace를 생성 기준으로 사용 |

---

## Related Skills

| Skill | 관계 |
|-------|------|
| `63-build-runner` | 빌드 실행 |
| `70-common-domain-policy` | common 도메인 정책 |

---

## Version History

| Version | Date | Changes |
|---------|------|---------|
| 0.3.0 | 2024-12-25 | 입력 폴더 구조 변경 (input/{domain}/...) |
| 0.2.0 | 2024-12-24 | v5 스펙 확정 |
| 0.1.0 | 2024-12-20 | Initial |

---

## 한 줄 요약

**입력은 `input/{domain}/contracts|tables|protocols`, 빌드는 domains 순회로 실행.**
