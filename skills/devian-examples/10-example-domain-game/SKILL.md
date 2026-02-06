# 10-example-domain-game

Status: ACTIVE  
AppliesTo: v10

## SSOT

이 문서는 **Game 도메인 예제** (테이블/컨트랙트)를 설명한다.

---

## 목표

- `DomainKey = Game`의 테이블/컨트랙트가 **최소 예제**임을 명시한다.
- 예제를 빌드하면 어떤 생성물이 만들어지는지 개념을 안내한다.
- 상세 규칙은 기존 스킬로 연결한다 (새 규칙 추가 금지).

---

## Game 도메인 예제

**Game 테이블/컨트랙트는 Devian 프레임워크의 최소 작동 예제다.**

### 입력 파일

| 유형 | 경로 | 설명 |
|------|------|------|
| Tables | `devian/input/Domains/Game/tables/TestTable.xlsx` | 테이블 예제 |
| Contracts | `devian/input/Domains/Game/contracts/TestContract.json` | 컨트랙트 예제 |

### 빌드 생성물

예제를 빌드하면 아래 생성물이 만들어진다:

| 플랫폼 | 생성물 | 경로 |
|--------|--------|------|
| C# Module | `Devian.Domain.Game` | `framework-cs/module/Devian.Domain.Game/` |
| UPM Package | `com.devian.domain.game` | `framework-cs/upm/com.devian.domain.game/` |
| TS Module | `devian-domain-game` | `framework-ts/module/devian-domain-game/` |
| Data (ndjson) | `Game/*.json` | `output/table/Game/ndjson/` |
| Data (pb64) | `Game/*.asset` | `output/table/Game/pb64/` |

---

## 빌드 흐름 개념

```
input/Domains/Game/tables/*.xlsx
input/Domains/Game/contracts/*.json
        ↓
   [build.js]
        ↓
  staging (tempDir)
        ↓
  framework-cs/module/Devian.Domain.Game/
  framework-cs/upm/com.devian.domain.game/
  framework-ts/module/devian-domain-game/
        ↓
  UnityExample/Packages/com.devian.domain.game/  (sync)
```

> **상세 흐름:** `skills/devian-unity/02-unity-bundles/SKILL.md` 참조

---

## 관련 스킬

| 주제 | 스킬 경로 |
|------|-----------|
| 도메인 패키지 공통 규약 | `skills/devian-unity/06-domain-packages/com.devian.domain.template/SKILL.md` |
| 테이블 작성 규칙 | `skills/devian-data/30-table-authoring-rules/SKILL.md` |
| 테이블 생성 구현 | `skills/devian-data/42-tablegen-implementation/SKILL.md` |
| 컨트랙트 생성 구현 | `skills/devian-data/43-contractgen-implementation/SKILL.md` |
| UPM 번들/복사 흐름 | `skills/devian-unity/02-unity-bundles/SKILL.md` |

---

## 금지

- 예제 스키마/내용 추가/변경 금지
- 새로운 빌드 규칙 추가 금지 (기존 스킬로 연결만)
- 이 문서는 "안내/연결" 역할만 담당

---

## Reference

- Related: `skills/devian-examples/01-policy/SKILL.md`
- Related: `skills/devian-unity/06-domain-packages/com.devian.domain.template/SKILL.md`
- Related: `skills/devian-core/03-ssot/SKILL.md`
