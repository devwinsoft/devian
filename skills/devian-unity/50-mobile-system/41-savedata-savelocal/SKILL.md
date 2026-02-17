# 41-savedata-savelocal — SaveLocal (Internal)


## Purpose
- 로컬 파일 기반 저장/불러오기 기능 (AES 암호화, atomic write 지원).
- **SaveLocalManager는 삭제됨.** 모든 로컬 저장 로직은 `SaveDataManager`의 private 메서드로 통합되었다.
- 외부에서 직접 호출 불가. `SaveDataManager.SyncAsync` / `ResolveConflictAsync`를 통해 간접 사용.


## Locations (mirrored)
- UPM:
  - `framework-cs/upm/com.devian.samples/Samples~/MobileSystem/Runtime/SaveData/SaveLocal/SaveLocalTypes.cs`
  - `framework-cs/upm/com.devian.samples/Samples~/MobileSystem/Runtime/SaveData/SaveLocal/SaveLocalPayload.cs`
  - `framework-cs/upm/com.devian.samples/Samples~/MobileSystem/Runtime/SaveData/SaveLocal/SaveLocalCrypto.cs`
  - `framework-cs/upm/com.devian.samples/Samples~/MobileSystem/Runtime/SaveData/SaveLocal/SaveLocalFileStore.cs`
  - `framework-cs/module/Devian/src/Core/Crypto.cs`
- UnityExample mirror (직접 수정 금지):
  - `framework-cs/apps/UnityExample/Packages/com.devian.samples/Samples~/MobileSystem/...`


## Assembly Definition (asmdef)
- 단일 asmdef(`Devian.Samples.CloudClientSystem`)에 포함되어 MobileSystem 번들 샘플과 함께 설치된다.


## What it does
- `SaveDataManager` 내부 private 메서드가 파일 I/O를 수행한다(slot 단위 저장/불러오기).
- `SaveLocalCrypto`로 SHA-256 체크섬을 생성/검증한다.
- `Crypto`로 AES 암호화/복호화를 수행한다.
- `SaveLocalFileStore`가 atomic write(임시 파일 → rename)로 파일을 안전하게 기록한다.


## Non-goals
- Cloud Save(서버 저장)는 이 스킬 범위 밖이다. → [42-savedata-savecloud](../42-savedata-savecloud/SKILL.md)


## Links
- [37-savedata-manager](../37-savedata-manager/SKILL.md) (진입점)
- [50-mobile-system overview](../00-overview/SKILL.md)
- [42-savedata-savecloud](../42-savedata-savecloud/SKILL.md)
