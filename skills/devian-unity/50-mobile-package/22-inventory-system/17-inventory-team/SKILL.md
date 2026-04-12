# 17-inventory-team


Status: ACTIVE
AppliesTo: v10
Type: Analysis / Design


## Overview


InventoryTeam은 hero team/loadout 상태를 inventory aggregate 안에서 관리하는 확장 설계다.
이 문서는 `AbilityTeam`를 도입하되, 현재 코드베이스에서는 `SelectedTeam`, `JoinedHeroes`, `NotJoinedHeroes`, `UnownedHeroes`를 `InventoryManager`가 직접 관리하는 안을 우선 권장한다.

- 팀 수: `3`
- 팀 슬롯 수: `AbilityTeam.SlotCount = 3`
- team 변경 알림은 inventory message의 단일 key `TEAM_LIST_CHANGED`로 통합한다


---


## Decision


### 권장 구조

- `AbilityTeam`는 `GamePackage`의 순수 runtime 모델이다.
- `InventoryStorage` / `InventorySnapshot`이 team raw state를 저장한다.
- `InventoryManager`가 team mutation/query/message boundary를 가진다.
- `SelectedTeam`, `JoinedHeroes`, `NotJoinedHeroes`, `UnownedHeroes`는 `InventoryManager`의 derived view다.


### 왜 `InventoryManager`가 관리하는가

- team은 독립 도메인이 아니라 owned hero 집합 위의 loadout projection이다.
- hero 보유/삭제/복원과 team slot 정합성을 같은 트랜잭션 경계에서 처리해야 한다.
- `CreateSnapshot` / `ReplaceState` / `ClearState`가 이미 inventory bulk boundary라서 team 저장도 같은 경계에 두는 편이 자연스럽다.
- message router가 이미 inventory에 있으므로 team 변경도 같은 trigger에 실어 보내는 편이 구독 모델이 단순하다.


### 왜 별도 `TeamManager : CompoSingleton`를 기본안으로 두지 않는가

- hero 정본은 `InventoryManager`, team 정본은 `TeamManager`가 되면 aggregate가 둘로 갈라진다.
- snapshot load 순서와 partial failure rollback이 복잡해진다.
- team 변경이 inventory message와 별도 message bus로 갈라질 가능성이 크다.
- `JoinedHeroes` / `NotJoinedHeroes` / `UnownedHeroes`가 결국 inventory hero set에 의존하므로 manager를 나눠도 파생 view 계산 책임은 강하게 결합된다.

정리:
- 스킬 이름은 `InventoryTeam(TeamManager)`로 유지할 수 있다.
- 하지만 실제 구현 기본안은 "team 기능을 inventory 시스템 내부 aggregate로 유지"다.


---


## Core Model


### 1) `AbilityTeam`

위치 권장:
- `GamePackage/Runtime/Ability/AbilityTeam.cs`

역할:
- hero 3명을 slot index 기준으로 보관하는 lightweight runtime
- 직접 저장 정본이 아니라 inventory snapshot으로 serialize되는 runtime view

권장 API:

```csharp
public sealed class AbilityTeam
{
    public const int SlotCount = 3;

    public AbilityItemHero GetHero(int slotIndex);
    public bool SetHero(int slotIndex, AbilityItemHero hero);
    public bool RemoveHero(int slotIndex);
    public bool Contains(string heroId);
    public IReadOnlyList<AbilityItemHero> GetHeroes();
}
```

규칙:
- slot index 범위는 `0..2`
- 같은 team 안에서 중복 hero 금지
- 빈 슬롯은 `null`


### 2) Inventory raw state

저장 위치 권장:
- `InventoryStorage`
- `InventorySnapshot`

필수 raw state:
- `AbilityTeam` 3개
- `SelectedTeamIndex`

snapshot에는 runtime reference 대신 `heroId(item_id)`만 저장한다.

예시 개념:

```csharp
public sealed class InventorySnapshotTeam
{
    public Dictionary<int, string> Slots { get; } = new();
}

public sealed class InventorySnapshot
{
    public int SelectedTeamIndex { get; set; }
    public List<InventorySnapshotTeam> Teams { get; } = new();
}
```


---


## Derived Views


아래 4개는 raw state가 아니라 `InventoryManager`가 재계산하는 파생 view다.

- `SelectedTeam -> AbilityTeam`
- `JoinedHeroes -> IReadOnlyList<AbilityItemHero>`
- `NotJoinedHeroes -> IReadOnlyList<AbilityItemHero>`
- `UnownedHeroes -> IReadOnlyList<ITEM_HERO>`

해석 기준:
- `JoinedHeroes`: 현재 `SelectedTeam`의 slot에 들어 있는 owned hero runtime
- `NotJoinedHeroes`: 현재 inventory에 owned 상태지만 `SelectedTeam`에는 들어 있지 않은 hero runtime
- `UnownedHeroes`: `TB_ITEM_HERO.GetAll()` 중 현재 `_storage.Heroes`에 없는 `item_id` row

중요:
- `JoinedHeroes` / `NotJoinedHeroes`는 "선택된 팀 기준"이다.
- 전체 3개 팀 합집합 기준 roster가 필요하면 별도 API를 추가한다. 이 리스트 의미를 확장해서 재사용하지 않는다.


---


## InventoryManager Contract


`InventoryManager`에 둘 권장 API:

```csharp
public sealed class InventoryManager : CompoSingleton<InventoryManager>
{
    public IReadOnlyList<AbilityTeam> Teams { get; }
    public int SelectedTeamIndex { get; }
    public AbilityTeam SelectedTeam { get; }

    public IReadOnlyList<AbilityItemHero> JoinedHeroes { get; }
    public IReadOnlyList<AbilityItemHero> NotJoinedHeroes { get; }
    public IReadOnlyList<ITEM_HERO> UnownedHeroes { get; }

    public GameResult SelectTeam(int teamIndex) { ... }
    public GameResult SetTeamHero(int teamIndex, int slotIndex, string heroId) { ... }
    public GameResult RemoveTeamHero(int teamIndex, int slotIndex) { ... }
}
```

보조 규칙:
- `SelectTeam(int)`는 `SelectedTeamIndex`만 바꾸고 파생 list를 재계산한다.
- `SetTeamHero(...)`는 hero ownership 검증 후 slot을 갱신한다.
- hero가 inventory에서 제거되면 모든 team slot에서 해당 hero를 자동 제거한다.
- `ReplaceState` / `ClearState` / hero add/remove 후에는 team validity를 정리한 다음 파생 list를 다시 만든다.
- full team system 이전의 최소 selection raw state로는 `InventoryManager.SelectedHeroId` + 파생 `SelectedHero`을 둘 수 있다.


---


## Message Design


### 권장안: 단일 inventory message key

team 관련 inventory message는 1개로 통합한다.

- key: `INVENTORY_MESSAGE_TYPE.TEAM_LIST_CHANGED`

payload 권장:

- `args[0] = int selectedTeamIndex`
- `args[1] = AbilityTeam selectedTeam`
- `args[2] = IReadOnlyList<AbilityItemHero> joinedHeroes`
- `args[3] = IReadOnlyList<AbilityItemHero> notJoinedHeroes`
- `args[4] = IReadOnlyList<ITEM_HERO> unownedHeroes`

이유:
- UI는 개별 delta보다 현재 team panel 전체를 다시 bind하는 경우가 많다.
- `SelectedTeam` 변경, slot 편집, hero 보유 변화가 모두 같은 화면 갱신 원인이다.
- `SELECTED_TEAM_CHANGED`, `JOINED_HEROES_CHANGED`, `NOT_JOINED_HEROES_CHANGED`, `UNOWNED_HEROES_CHANGED`로 나누면 구독 조합이 과해진다.

주의:
- team raw state 변경이 없어도 hero 보유 변화 때문에 파생 list가 달라질 수 있다.
- 따라서 `TEAM_LIST_CHANGED`는 "raw team changed"가 아니라 "team-related derived view changed" 의미로 본다.


---


## Validation Rules


- team index 범위: `0..2`
- slot index 범위: `0..2`
- 비보유 hero는 slot에 넣을 수 없다
- 같은 team 안에서 동일 hero 중복 금지
- 다른 team 간 hero 중복은 우선 허용 권장
- `SelectedTeamIndex`는 항상 유효 범위여야 한다
- snapshot load 시 존재하지 않는 heroId를 참조하는 slot은 비운다


---


## Refresh Pattern


장비의 `refreshEquipViews()`와 같은 방식으로 team 관련 파생 view를 재계산한다.

권장 helper:

```csharp
void refreshTeamViews()
{
    // SelectedTeam
    // JoinedHeroes
    // NotJoinedHeroes
    // UnownedHeroes
}
```

호출 시점:
- `onInitAwake`
- `ApplyHero` / `AddHeroAmount` / `RevokeHero`
- `SelectTeam`
- `SetTeamHero` / `RemoveTeamHero`
- `ReplaceState`
- `ClearState`


---


## Implementation Notes


- `AbilityTeam`는 inventory system 하위 스킬이지만 실제 클래스 위치는 `GamePackage`가 맞다. `AbilityItemHero` runtime을 직접 참조하기 때문이다.
- `InventoryStorage`는 raw state만 가진다. `JoinedHeroes` / `NotJoinedHeroes` / `UnownedHeroes`를 저장하지 않는다.
- `InventorySnapshot`은 선택 팀과 team slot hero id만 저장한다.
- inventory message는 기존 `InventoryMessageTrigger`를 계속 사용한다.


---


## Related

- [10-inventory-manager](../10-inventory-manager/SKILL.md)
- [11-inventory-storage](../11-inventory-storage/SKILL.md)
- [16-inventory-message-trigger](../16-inventory-message-trigger/SKILL.md)
- [12-game-ability](../../../21-game-package/12-game-ability/SKILL.md)
