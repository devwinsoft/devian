# 11-treasure-storage

Status: ACTIVE
AppliesTo: v10
Type: Design / Storage SSOT

`TreasureStorage` 저장 모델과 상태 규약 문서다.
`TreasureManager`가 storage를 소유한다.

---

## Implementation Location (target 3-path mirror)

- UPM (정본):
  `framework-cs/upm/com.devian.samples/Samples~/MobileSystem/Runtime/Treasure/TreasureStorage.cs`
- Packages (sync):
  `framework-cs/apps/UnityExample/Packages/com.devian.samples/Samples~/MobileSystem/Runtime/Treasure/TreasureStorage.cs`
- Assets/Samples (import):
  `framework-cs/apps/UnityExample/Assets/Samples/Devian Samples/{version}/MobileSystem/Runtime/Treasure/TreasureStorage.cs`

---

## Ownership

- `TreasureManager`가 storage를 생성/소유한다.
- 외부 공개 경계: `TreasureManager.Storage`
- direct mutation은 `TreasureManager` 내부 경로만 허용한다.

---

## Storage Model

```csharp
public sealed class TreasureStorageCurrent
{
    public int Exp { get; set; }
    public int Level { get; set; } = 1;

    public void Reset(int level = 1, int exp = 0);
    public void Clear();
}

public sealed class TreasureStorage
{
    public int SchemaVersion { get; set; } = 1;
    public Dictionary<TREASURE_GRADE_TYPE, int> ChestCounts { get; } = new();
    public TreasureStorageCurrent Current { get; } = new();
}
```

의미:

- `ChestCounts`: grade별 보유 chest count
- `Current`: current 상태 (exp/level)를 묶는 하위 객체
- `Current.Exp`: treasure exp
- `Current.Level`: 현재 treasure reward level

---

## Default Rules

- `Current.Level` 기본값은 `1`
- `Current.Exp` 기본값은 `0`
- 미존재 grade key는 `0개`로 간주한다
- `NONE` grade는 저장 대상이 아니다

---

## Recommended Helper API

- `GetChestCount(TREASURE_GRADE_TYPE gradeType) -> int`
- `AddChest(TREASURE_GRADE_TYPE gradeType, int amount)`
- `SetChestCount(TREASURE_GRADE_TYPE gradeType, int count)`
- `AddCurrentExp(int amount)` — delegates to `Current.Exp`
- `ResetCurrent(level = 1, exp = 0)` — delegates to `Current.Reset(...)`
- `Clear()` — clears all including `Current.Clear()`

---

## Hard Rules

- `TreasureStorage`와 `TreasureStorageCurrent`는 sealed POCO 클래스다.
- chest count와 exp는 음수가 될 수 없다.
- current 상태는 `TreasureStorageCurrent`로 묶는다. grade별로 분리하지 않는다. 단일 `Current.Exp` / `Current.Level`만 사용한다.
- max level 판단은 storage가 아니라 `TREASURE_CHEST` 테이블을 기준으로 한다.
- level wrap 규칙 적용 주체는 `TreasureManager`다.

---

## Target Save Shape

```json
{
  "schemaVersion": 1,
  "current": {
    "exp": 42,
    "level": 2
  },
  "chestCounts": {
    "COMMON": 3,
    "EPIC": 1
  }
}
```

이 문서는 저장 모델만 정의한다.
실제 SaveData codec 통합은 후속 단계다.

---

## Related

- [03-ssot](../03-ssot/SKILL.md)
- [10-treasure-manager](../10-treasure-manager/SKILL.md)
- [21-savedata-system/43-savedata-json-codec](../../21-savedata-system/43-savedata-json-codec/SKILL.md)
