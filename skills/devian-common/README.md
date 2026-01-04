# Devian Common Skills

Common 도메인 및 그 산출물에 대한 스킬 문서.

## 스킬 목록

| 스킬 | 설명 |
|------|------|
| `00-skill-specification` | Common 스킬 전반 규약 |
| `12-common-standard-types` | 공용 표준 타입 (`G*` prefix) |
| `70-common-domain-policy` | Common 도메인 기본 정책 |

## 주요 경로

### 입력 (도메인)

```
input/Common/
├── contracts/   # 계약 정의 (*.json)
└── tables/      # 테이블 정의 (*.xlsx)
```

### 산출물

**Common 도메인 빌드 산출물의 정본 위치는 `framework/` 이다.**

```
framework/
├── cs/Devian.Module.Common/
│   └── generated/   # 기계 생성 (커밋 필수, 수정 금지)
├── ts/devian-module-common/
│   └── generated/   # 기계 생성 (커밋 필수, 수정 금지)
└── data/Common/
    └── json/        # 테이블 데이터 (NDJSON)
```

## 참조

- Common 도메인: `input/Common/`
- Common 산출물: `framework/`
- 상위 스킬: `skills/devian/`
