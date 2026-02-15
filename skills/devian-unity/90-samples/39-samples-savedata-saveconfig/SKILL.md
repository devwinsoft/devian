# 39-samples-savedata-saveconfig — SaveSlotConfig (File Separation)


## Purpose
SaveDataManager에서 SaveSlot / SaveSlotConfig 타입을 별도 파일로 분리하여,
단일 파일 비대화를 방지하고 관심사를 명확히 분리한다.


## Files

| File | Description |
|------|-------------|
| `SaveSlot.cs` | 슬롯 데이터: `slotKey`, `filename` (Local), `cloudSlot` (Cloud) |
| `SaveSlotConfig.cs` | 슬롯/암호화 설정 캡슐화 (Inspector 설정 위임) |
| `SaveDataManager.cs` | 단일 매니저 — 위 타입을 필드로 사용 |


## Location (Code)
- `framework-cs/upm/com.devian.samples/Samples~/MobileSystem/Runtime/SaveData/SaveSlot.cs`
- `framework-cs/upm/com.devian.samples/Samples~/MobileSystem/Runtime/SaveData/SaveSlotConfig.cs`

(UPM ↔ UnityExample/Packages ↔ UnityExample/Assets/Samples 미러에 동일 존재)


## Notes
- SaveSlotConfig는 `SaveDataManager.IsValidJsonFilename()` (internal static)을 호출한다.
- SaveSlot, SaveSlotConfig 모두 `namespace Devian` 최상위 타입이다 (중첩 아님).
- [38-samples-savedata-saveslot](../38-samples-savedata-saveslot/SKILL.md)은 SlotConfig의 인터페이스/동작을 기술하고, 이 문서는 파일 분리 결과를 기술한다.
