# 42-samples-savedata-savecloud — SaveCloud (Internal)


## Purpose
- Android(GPGS) / iOS(iCloud) 공통 클라우드 저장 기능을 제공한다.
- **SaveCloudManager는 삭제됨.** 모든 클라우드 로직은 `SaveDataManager`의 private/internal 메서드로 통합되었다.
- **Editor/Guest에서는 CloudSave를 사용하지 않는다(Failure 반환).**
- 외부 진입점: `SaveDataManager.Instance._initializeCloudAsync(ct)` (AccountManager에서 호출).


## Locations (mirrored)
- UPM:
  - `framework-cs/upm/com.devian.samples/Samples~/MobileSystem/Runtime/SaveData/SaveCloud/AppleSaveCloudClient.cs`
  - `framework-cs/upm/com.devian.samples/Samples~/MobileSystem/Runtime/SaveData/SaveCloud/SaveCloudTypes.cs`
  - `framework-cs/upm/com.devian.samples/Samples~/MobileSystem/Runtime/SaveData/SaveCloud/ISaveCloudClient.cs`
  - `framework-cs/upm/com.devian.samples/Samples~/MobileSystem/Runtime/SaveData/SaveCloud/SaveCloudClientGoogle.cs`
  - `framework-cs/upm/com.devian.samples/Samples~/MobileSystem/Runtime/SaveData/SaveCloud/SaveCloudClient.cs`
  - `framework-cs/upm/com.devian.samples/Samples~/MobileSystem/Runtime/SaveData/SaveCloud/SaveCloudCrypto.cs`
  - `framework-cs/upm/com.devian.samples/Samples~/MobileSystem/Runtime/SaveData/SaveCloud/SaveCloudPayload.cs`
  - `framework-cs/upm/com.devian.samples/Samples~/MobileSystem/Runtime/SaveData/SaveCloud/SaveCloudResult.cs`
- UnityExample mirror (직접 수정 금지):
  - `framework-cs/apps/UnityExample/Packages/com.devian.samples/Samples~/MobileSystem/...`


## Assembly Definition (asmdef)
- 단일 asmdef(`Devian.Samples.CloudClientSystem`)에 포함되어 MobileSystem 번들 샘플과 함께 설치된다.


## Platform behavior
- Editor: CloudSave는 지원되지 않는다(`CLOUDSAVE_NOCLIENT` Failure 반환). LocalSave만 사용한다.
- Android: `SaveCloudClientGoogle`(GPGS)가 기본 선택된다.
- iOS: `AppleSaveCloudClient`(iCloud)가 기본 선택된다.


## What it does
- `SaveDataManager.Instance._initializeCloudAsync(ct)`로 플랫폼별 client 선택 및 초기화를 수행한다.
- 정책: **Unity Editor에서는 CloudSave를 사용하지 않는다(SaveDataManager가 Failure 반환).**
- Key/IV 관리는 SaveDataManager Inspector에서 통합 설정.


## Usage
- **직접 호출 불가.** SaveCloudManager는 삭제됨.
- `AccountManager.LoginAsync` → 내부에서 `SaveDataManager.Instance._initializeCloudAsync(ct)` 호출.
- Sync/Resolve는 `SaveDataManager.SyncAsync` / `ResolveConflictAsync`를 통해 간접 사용.


## Non-goals
- 로그인/인증 플로우 → [33-samples-account-manager](../33-samples-account-manager/SKILL.md)
- 로컬 저장 → [41-samples-savedata-savelocal](../41-samples-savedata-savelocal/SKILL.md)
- 트리거/주기, 재시도/충돌 정책 같은 비즈니스 로직


## Notes
- iOS에서는 `AppleSaveCloudClient`(iCloud)가 사용된다.
- Editor에서는 CloudSave를 사용하지 않으며, `SaveDataManager`가 Failure를 반환한다(SSOT).
- iCloud 구현 상세: [20-save-system — 25-cloudsave-firebase](../../20-save-system/25-cloudsave-firebase/SKILL.md)


## Links
- [37-samples-savedata-manager](../37-samples-savedata-manager/SKILL.md) (진입점)
- [30-samples-mobile-system](../30-samples-mobile-system/SKILL.md)
- [41-samples-savedata-savelocal](../41-samples-savedata-savelocal/SKILL.md)
