# 20-save-system — Cloud Save Firebase (Unity)


## What it is




`FirebaseCloudSaveClient`는 Firestore 기반 저장을 제공한다.




- 사용자 식별: `FirebaseAuth` (Anonymous sign-in)
- 저장 위치:
  - `users/{uid}/cloudsave/{slot}`
- 슬롯 단위 저장:
  - 문서 1개 = 슬롯 1개
- 저장 필드(문서):
  - `Version` (int)
  - `UpdateTime` (string)
  - `UtcTime` (long)
  - `Payload` (string)
  - `Checksum` (string)




---




## Runtime behavior




- `SignInIfNeededAsync`:
  - Firebase dependencies 체크 후(`CheckAndFixDependenciesAsync`)
  - `SignInAnonymouslyAsync` 수행
- `SaveAsync`:
  - 슬롯 문서에 payload 메타/데이터 저장(`SetAsync(..., MergeAll)`)
- `LoadAsync`:
  - 슬롯 문서를 읽어 payload 복원
- `DeleteAsync`:
  - 슬롯 문서 삭제




---




## How to use (iOS now)




iOS에서 당장 iCloud 구현이 준비되지 않은 경우, **Installer에서 Firebase client를 주입**해 사용한다.




- 샘플 엔트리:
  - `Samples~/SaveSystem/Runtime/ClaudSaveInstaller.cs`
- iOS 분기에서:
  - `CloudSaveManager.Instance.Configure(client: new FirebaseCloudSaveClient())`




주의:
- `AppleCloudSaveClient`(iCloud)는 "설계대로 유지"하며, 준비 완료 후 iOS 분기 주입을 교체한다.




---




## Prerequisites (Unity)




Firebase Unity SDK가 프로젝트에 포함되어 있어야 한다.




- 최소 요구(예시):
  - Firebase Core
  - Firebase Auth
  - Firebase Firestore




이 문서는 SDK 설치/프로젝트 설정 자체는 다루지 않는다.
(설치가 없으면 `Firebase.*` 네임스페이스 관련 컴파일 에러가 발생할 수 있다.)




---




## Notes




- 플랫폼 네이티브 저장소(GPGS/iCloud)와의 크로스플랫폼 세이브 공유는 비목표다.
- Firebase 저장소는 "iOS 임시/대체 구현" 또는 "향후 통합 백엔드" 옵션으로 사용한다.
