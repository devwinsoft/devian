# 42-savedata-savecloud — SaveCloud (Internal)


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
- 로그인/인증 플로우 → [33-account-manager](../33-account-manager/SKILL.md)
- 로컬 저장 → [41-savedata-savelocal](../41-savedata-savelocal/SKILL.md)
- 트리거/주기, 재시도/충돌 정책 같은 비즈니스 로직


## Notes
- iOS에서는 `AppleSaveCloudClient`(iCloud)가 사용된다.
- Editor에서는 CloudSave를 사용하지 않으며, `SaveDataManager`가 Failure를 반환한다(SSOT).
- iCloud 구현 상세: 아래 "Cloud Save Firebase" 섹션 참조


## Cloud Save Firebase (Firestore) — Inline Reference

`SaveCloudClient`는 Firestore 기반 저장을 제공한다.

- 사용자 식별: `FirebaseAuth` (Anonymous sign-in)
- 저장 위치:
  - `users/{uid}/cloudsave/{slot}`
- 슬롯 단위 저장:
  - 문서 1개 = 슬롯 1개
- 저장 필드(문서):
  - `Version` (int)
  - `UpdateTime` (string)
  - `Payload` (string)
  - `DeviceId` (string)
  - *(legacy `UtcTime`/`Checksum` 필드는 SaveAsync 시 `FieldValue.Delete`로 제거)*

### Runtime behavior

- `SignInIfNeededAsync`:
  - Firebase dependencies 체크 후(`CheckAndFixDependenciesAsync`)
  - `SignInAnonymouslyAsync` 수행
- `SaveAsync`:
  - 슬롯 문서에 payload 메타/데이터 저장(`SetAsync(..., MergeAll)`)
- `LoadAsync`:
  - 슬롯 문서를 읽어 payload 복원
  - `SaveCloudResult.NotFound`는 "클라우드 데이터 없음(첫 저장 전)"을 의미하며, Sync에서는 실패가 아닌 `Success(null)`로 취급한다.
- `DeleteAsync`:
  - 슬롯 문서 삭제

### How to use (iOS)

정책: **Unity Editor에서는 CloudSave를 사용하지 않는다(LocalSave only).** SaveCloudManager가 Failure를 반환한다.
iOS 런타임 빌드에서 iCloud 미구현 시에만, 프로젝트 요구에 따라 `SaveCloudManager.Instance.Configure(client: new SaveCloudClient())` 방식으로 client를 주입한다.

주의:
- `AppleSaveCloudClient`(iCloud)는 "설계대로 유지"하며, 준비 완료 후 iOS 분기 주입을 교체한다.

### Prerequisites (Unity)

Firebase Unity SDK가 프로젝트에 포함되어 있어야 한다.

- 최소 요구(예시):
  - Firebase Core
  - Firebase Auth
  - Firebase Firestore

이 문서는 SDK 설치/프로젝트 설정 자체는 다루지 않는다.
(설치가 없으면 `Firebase.*` 네임스페이스 관련 컴파일 에러가 발생할 수 있다.)

### Firebase Notes

- `SaveCloudClient`는 CloudSave 저장소 접근만 담당하며, 로그인(anonymous 포함)은 AccountManager/FirebaseManager가 선행되어야 한다. SaveCloudClient 내부에서 별도 sign-in을 시도하지 않는다.
- 플랫폼 네이티브 저장소(GPGS/iCloud)와의 크로스플랫폼 세이브 공유는 비목표다.
- Firebase 저장소는 "Editor/iOS 임시/대체 구현" 또는 "향후 통합 백엔드" 옵션으로 사용한다.


## Links
- [37-savedata-manager](../37-savedata-manager/SKILL.md) (진입점)
- [50-mobile-system overview](../00-overview/SKILL.md)
- [41-savedata-savelocal](../41-savedata-savelocal/SKILL.md)
