# devian-unity/21-game-package — Overview

Status: ACTIVE
AppliesTo: v10

GamePackage는 **Devian Foundation**(`com.devian.foundation`)에 포함된 Unity Sample이다.
이 그룹은 `devian/21-domain-game`에서 생성된 Game 도메인 타입 위에 얹히는 Unity 수동 addon 코드와 GamePackage 전용 generated 소비 규약을 담당한다.

---

## Sample SSOT

- `com.devian.foundation/Samples~/GamePackage`

---

## Scope

- Manual addon: `Runtime/Ability/*`
- Manual addon: `Runtime/RedDot/*`
- Game StringTable consumer: `GAME_TEXT_ID`, `ST_GAME_TEXT`
- Generated runtime: `Runtime/Generated/*` — 정본은 `devian/21-domain-game`
- Generated editor bindings: `Editor/Generated/*` — 정본은 `devian/21-domain-game`

---

## Start Here

| Document | Description |
|----------|-------------|
| [14-red-dot-system](../14-red-dot-system/00-overview/SKILL.md) | `Runtime/RedDot/*` red dot 상태/전파/이벤트 시스템 |
| [12-game-ability](../12-game-ability/SKILL.md) | AbilityBase/Battle/Affect/Equip/Card/UnitHero/UnitMonster 수동 addon |
| [15-game-ability-factory](../15-game-ability-factory/SKILL.md) | `AbilityItem*`, `AbilityUnit*`, preview projection 생성 규약 |
| [13-game-string-table](../13-game-string-table/SKILL.md) | `GameStringTable.xlsx` → `GAME_TEXT` → `GAME_TEXT_ID`/`ST_GAME_TEXT` |

---

## Related

- [devian/21-domain-game/00-overview](../../../devian/21-domain-game/00-overview/SKILL.md) — Game 도메인 생성 규약
- [devian/21-domain-game/11-game-tables](../../../devian/21-domain-game/11-game-tables/SKILL.md) — Game 테이블 입력/생성물
- [devian/21-domain-game/13-game-stat-type](../../../devian/21-domain-game/13-game-stat-type/SKILL.md) — `STAT_TYPE` enum
- [devian-unity/20-common-package/30-string-table](../../20-common-package/30-string-table/SKILL.md) — StringTable 공통 규약
- [devian-unity/02-unity-bundles](../../02-unity-bundles/SKILL.md) — `Devian.Samples.GamePackage` asmdef/의존 방향
- [devian-unity/04-package-policy](../../04-package-policy/SKILL.md) — Samples~ 패키지 정책
