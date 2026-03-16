# 41-savedata-savelocal — SaveLocal (Internal)


## Purpose
- 로컬 파일 기반 저장/불러오기 기능 (ComplexUtil 난독화, atomic write 지원).
- **SaveLocalManager는 삭제됨.** 모든 로컬 저장 로직은 `SaveDataManager`의 private 메서드로 통합되었다.
- 외부에서 직접 호출 불가. `SaveDataManager.SyncGameStorageAsync` / `ResolveConflictAsync`를 통해 간접 사용.


## Implementation Location (3-path mirror)
- UPM (정본):
  - `framework-cs/upm/com.devian.samples/Samples~/MobilePackage/Runtime/SaveData/SaveLocal/SaveLocalTypes.cs`
  - `framework-cs/upm/com.devian.samples/Samples~/MobilePackage/Runtime/SaveData/SaveLocal/SaveLocalPayload.cs`
  - `framework-cs/upm/com.devian.samples/Samples~/MobilePackage/Runtime/SaveData/SaveLocal/SaveLocalCrypto.cs`
  - `framework-cs/upm/com.devian.samples/Samples~/MobilePackage/Runtime/SaveData/SaveLocal/SaveLocalFileStore.cs`
- Packages (sync, 직접 수정 금지):
  - `framework-cs/apps/UnityExample/Packages/com.devian.samples/Samples~/MobilePackage/...`


## Assembly Definition (asmdef)
- 단일 asmdef(`Devian.Samples.MobilePackage`)에 포함되어 MobilePackage 번들 샘플과 함께 설치된다.


## SaveLocalPayload Fields
- `version` (int): 스키마 버전
- `updateTime` (string): 저장 시점(표시/진단용; Sync 최신성 판정 기준 아님)
- `payload` (string): 난독화된 게임 데이터 (Base64)
- `deviceId` (string): 디바이스 식별자
- `saveSeq` (long): 기기별 단조 증가 저장 순번 (same-device Sync 최신성 판정용)
- `account` (`AccountStorage`): 계정 메타 미러 (`loginType`, `socialUserId`, `lastUpdatedAtUtcMs`)


## What it does
- `SaveDataManager` 내부 private 메서드가 primary local file 기준으로 파일 I/O를 수행한다.
- `SaveLocalCrypto`로 SHA-256 체크섬을 생성/검증한다.
- `ComplexUtil`로 payload 난독화/역난독화를 수행한다 (경량 바이트 치환, Key/IV 불필요).
- `SaveLocalFileStore`가 atomic write(임시 파일 → rename)로 파일을 안전하게 기록한다.


## Non-goals
- Cloud Save(서버 저장)는 이 스킬 범위 밖이다. → [42-savedata-savecloud](../42-savedata-savecloud/SKILL.md)


## Links
- [10-savedata-manager](../10-savedata-manager/SKILL.md) (진입점)
- [50-mobile-package overview](../../00-overview/SKILL.md)
- [42-savedata-savecloud](../42-savedata-savecloud/SKILL.md)
