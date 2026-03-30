# devian/21-domain-game — Overview

Status: ACTIVE
AppliesTo: v10

**Game 도메인 정의.** DomainKey = `Game`의 테이블, 생성 enum, generated C#/TS 타입을 정의한다.

**UPM 생성물 위치:** `com.devian.foundation/Samples~/GamePackage/Runtime/Generated/`, `com.devian.foundation/Samples~/GamePackage/Editor/Generated/`
**Assembly:** `Devian.Samples.GamePackage` (Generated namespace: `Devian.Domain.Game`)
**Unity addon:** [devian-unity/21-game-package](../../../devian-unity/21-game-package/00-overview/SKILL.md)

---

## Sub-skills

- [11-game-tables](../11-game-tables/SKILL.md) — Game 도메인 테이블 정의
- [12-game-ability](../12-game-ability/SKILL.md) — Ability feature 모델 (TS/module 관점, generated 타입 소비)
- [13-game-stat-type](../13-game-stat-type/SKILL.md) — STAT_TYPE enum 정의 (ITEM_AMOUNT, ITEM_LEVEL 등)

---

## Related

- [30-protocol-game](../../30-protocol-game/SKILL.md) — Game 프로토콜 예제
- [Builder SSOT](../../80-tools/11-builder/03-ssot/SKILL.md) — 테이블/컨트랙트 빌드 규칙
- [devian-unity/21-game-package/00-overview](../../../devian-unity/21-game-package/00-overview/SKILL.md) — Unity GamePackage addon 코드
