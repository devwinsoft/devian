# GamePackage

Game 도메인 샘플 패키지. Ability 시스템(AbilityBase, AbilityEquip, AbilityCard, AbilityUnitHero, AbilityUnitMonster)과 Generated 테이블/enum 코드를 포함한다.

## 구성

- `Runtime/Ability/` — 수동 addon 코드 (Ability 시스템)
- `Runtime/Generated/` — 빌더가 자동 생성하는 Game 도메인 코드
- `Editor/Generated/` — 빌더가 자동 생성하는 TableID Editor 바인딩

## 의존

- `Devian.Core`
- `Devian.Domain.Common`
- `Devian.Domain.Sound`
