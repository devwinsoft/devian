# devian-tools/11-builder — Overview

> Routing(키워드→스킬)은 중앙 정본을 따른다: `skills/devian/00-overview/SKILL.md`

Devian 테이블, Contract, 스토리지 포맷, 프로토콜 코드젠 구현을 담당한다.

- **Table Authoring**: Excel 테이블 작성 규칙
- **Contract**: JSON Row I/O, Class Cell 포맷
- **Storage**: NDJSON, PB64 바이너리 포맷
- **Codegen**: TableGen, ContractGen, EnumGen 구현
- **Protocol Codegen**: 프로토콜 코드젠 규칙
- **Protocol C#/TS**: C#/TS 프로토콜 코드젠 상세
- **ProtocolGen**: 프로토콜 코드젠 구현
- **Build Domain**: 도메인/프로토콜 빌드 실행 정책
- **Build Error Reporting**: 빌드 오류 JSON 포맷

---

## Start Here

| Document | Description |
|----------|-------------|
| [01-policy](../01-policy/SKILL.md) | Builder 그룹 정책 |
| [03-ssot](../03-ssot/SKILL.md) | Builder SSOT (tableConfig, Tables, NDJSON, pb64) |
| [20-build-domain](../20-build-domain/SKILL.md) | 도메인 빌드 상세 |
| [21-build-error-reporting](../21-build-error-reporting/SKILL.md) | 빌드 오류 리포팅 |
| [30-table-authoring-rules](../30-table-authoring-rules/SKILL.md) | 테이블 작성 규칙 |
| [32-json-row-io](../32-json-row-io/SKILL.md) | JSON Row I/O |
| [34-ndjson-storage](../34-ndjson-storage/SKILL.md) | NDJSON 스토리지 |
| [40-codegen-protocol](../40-codegen-protocol/SKILL.md) | 프로토콜 코드젠 규칙 |
| [41-codegen-protocol-csharp-ts](../41-codegen-protocol-csharp-ts/SKILL.md) | C#/TS 코드젠 상세 |
| [44-protocolgen-implementation](../44-protocolgen-implementation/SKILL.md) | ProtocolGen 구현 |

---

## Related

- [Builder SSOT](../03-ssot/SKILL.md)
- [Root SSOT](../../../devian/10-module/03-ssot/SKILL.md)
- [Devian Index](../../../devian/SKILL.md)
