# 38-samples-savedata-saveslot — SaveData SlotConfig (SaveSlotConfig)


## Purpose
SaveDataManager의 슬롯/암호화(Encryption: Key/IV) 설정을 `SaveSlotConfig`로 분리하여,
Inspector에서 한 곳에서 관리하고, SaveDataManager 내부에서는 위임으로만 접근하도록 한다.


## Location (Code)
- `framework-cs/upm/com.devian.samples/Samples~/MobileSystem/Runtime/SaveData/SaveSlotConfig.cs`
- `framework-cs/upm/com.devian.samples/Samples~/MobileSystem/Runtime/SaveData/SaveSlot.cs`

(UPM ↔ UnityExample/Packages ↔ UnityExample/Assets/Samples 미러에 동일 존재)


## Inspector Setup
SaveDataManager 컴포넌트에서 다음을 설정한다:


- `Slot Config`
  - `Use Encryption` (bool)
  - `Key Base64` (CString)
  - `IV Base64` (CString)
  - `Slots` (List)
    - `slotKey`
    - `filename` (Local)
    - `cloudSlot` (Cloud)


## SlotConfig Interface (Methods)
SaveDataManager는 슬롯/암호화 관련 접근을 `SaveSlotConfig`로 위임한다.


- `bool UseEncryption { get; }`
- `List<string> GetLocalSlotKeys()`
- `List<string> GetCloudSlotKeys()`
- `bool TryResolveLocalFilename(string slotKey, out string filename)`
- `bool TryResolveCloudSlot(string slotKey, out string cloudSlot)`
- `void GetKeyIvBase64(out string keyBase64, out string ivBase64)`
- `CoreResult<bool> SetKeyIvBase64(string keyBase64, string ivBase64)`
- `void ClearKeyIv()`
- `bool TryGetKeyIv(out byte[] key, out byte[] iv, out string error)`


## Notes
- 이 샘플은 "슬롯/암호화 설정의 캡슐화"만 다루며, Sync/Load/Save 동작은 변경하지 않는다.
- SaveLocal/SaveCloud를 외부에서 직접 호출하지 않고, SaveDataManager를 단일 엔트리로 사용한다.
