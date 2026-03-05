# devian/80-tools/11-builder — Overview

> Routing(키워드→스킬)은 중앙 정본을 따른다: `skills/devian/00-overview/SKILL.md`

Devian 테이블, Contract, 스토리지 포맷, 프로토콜 코드젠 구현을 담당한다.

## Build Targets

빌드는 `-[target]` 옵션으로 실행 범위를 지정한다. 상세: [03-ssot § Build Target](../03-ssot/SKILL.md)

| Target | 설명 | 관련 문서 |
|--------|------|-----------|
| `-all` | 전체 빌드 (기본값) | 20-build-pipeline |
| `-domain-code` | Domain codegen (C#/TS) | 5x (50-52) |
| `-domain-data` | Domain 데이터 (NDJSON/pb64) | 5x (53-54) |
| `-protocol` | Protocol codegen (C#/TS) | 5x (55) |

---

## Start Here

| Document | Description |
|----------|-------------|
| [01-policy](../01-policy/SKILL.md) | Builder 그룹 정책 |
| [03-ssot](../03-ssot/SKILL.md) | Builder SSOT (Build Target, tableConfig, Tables, Protocol) |
| [20-build-pipeline](../20-build-pipeline/SKILL.md) | 빌드 실행 정책 |
| [21-build-error-reporting](../21-build-error-reporting/SKILL.md) | 빌드 오류 리포팅 |

### 3x — Input (소스 포맷/작성 규칙)

| Document | Description |
|----------|-------------|
| [30-table-cell-format](../30-table-cell-format/SKILL.md) | DFF (셀 텍스트 표현 규약) |
| [31-table-row-format](../31-table-row-format/SKILL.md) | Row→JSON 변환 규칙 |
| [32-table-authoring](../32-table-authoring/SKILL.md) | XLSX 테이블 작성 규칙 |
| [33-protocol-spec](../33-protocol-spec/SKILL.md) | 프로토콜 입력 포맷/규칙 |
| [34-protocol-gen-policy](../34-protocol-gen-policy/SKILL.md) | 프로토콜 C#/TS 산출물 정책 |

### 5x — Target (빌드 산출물)

| Document | Description |
|----------|-------------|
| [50-contract-codegen](../50-contract-codegen/SKILL.md) | Contract codegen |
| [51-table-codegen](../51-table-codegen/SKILL.md) | Table codegen |
| [52-table-enumgen](../52-table-enumgen/SKILL.md) | Enum codegen |
| [53-data-ndjson](../53-data-ndjson/SKILL.md) | NDJSON 스토리지 |
| [54-data-pb64](../54-data-pb64/SKILL.md) | PB64 스토리지 |
| [55-protocol-codegen](../55-protocol-codegen/SKILL.md) | Protocol codegen |

---

## Related

- [Builder SSOT](../03-ssot/SKILL.md)
- [Root SSOT](../../../10-module/03-ssot/SKILL.md)
- [Devian Index](../../../SKILL.md)
