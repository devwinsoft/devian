# Devian v10 — Table Data Export (NDJSON)

Status: ACTIVE  
AppliesTo: v10  
SSOT: skills/devian/03-ssot/SKILL.md

## Purpose

XLSX 테이블에서 추출된 데이터를 **NDJSON(Line-delimited JSON)**으로 내보내는 규약을 정의한다.

이 문서는 “산출 포맷과 경로”만 정의한다.
생성되는 로더/컨테이너 API는 **`docs/generated/devian-reference.md`**가 정답이다.

---

## NDJSON 규약

- 파일은 UTF-8 텍스트
- 한 줄이 한 레코드(JSON object)
- 빈 줄은 허용하지 않는 것을 권장

> JSON array가 아니라 NDJSON이 기본 산출물이다.

---

## Data Stop Rule (다시 강조)

PrimaryKey 컬럼이 빈 값이면 즉시 중단한다.

---

## Output Paths

SSOT 경로 규약을 따른다.

- staging: `{tempDir}/{DomainKey}/data/json/{sheetKey}.ndjson`
- final: `{dataTargetDir}/json/{sheetKey}.ndjson`

---

## Notes

- `enum:*` / `class:*` 컬럼의 셀 값은 DFF 원문 문자열로 보존될 수 있다.
- DFF 문법은 `skills/devian/25-class-cell-format/SKILL.md`를 따른다.

---

## Reference

- Policy SSOT: `skills/devian/03-ssot/SKILL.md`
- DFF 규약: `skills/devian/25-class-cell-format/SKILL.md`
- Code-based Reference: `docs/generated/devian-reference.md`