# GamePackage

Game 도메인 샘플 패키지. Ability 시스템(AbilityBase, AbilityItemEquip, AbilityItemCard, AbilityItemMaterial, AbilityItemHero, AbilityUnitHero, AbilityUnitMonster), Ability Factory, Generated 테이블/enum 코드를 포함한다.

## 구성

- `Runtime/Ability/` — 수동 addon 코드 (Ability 시스템)
- `Runtime/Ability/Factory/` — ability 생성 / projection factory
- `Runtime/Generated/` — 빌더가 자동 생성하는 Game 도메인 코드
- `Editor/Generated/` — 빌더가 자동 생성하는 TableID Editor 바인딩

## 의존

- `Devian.Core`
- `Devian.Domain.Common`
- `Devian.Domain.Sound`
