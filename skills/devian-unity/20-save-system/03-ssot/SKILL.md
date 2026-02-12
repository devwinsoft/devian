# 20-save-system — SSOT


Status: ACTIVE
AppliesTo: v10
SSOT: this file


---


## Purpose


이 문서는 20-save-system(Devian Unity 하위) 스킬의 정본(SSOT)이다.
Cloud Save와 Local Save(Unity) 모두 이 문서를 따른다.


---


## Scope


- Unity 전용
- 지원 플랫폼: Android(Google), iOS(iCloud), PC(Steam)


---


## Numbering Rules


- 10대: Cloud/Local 공통 스펙/정책
- 20대: Cloud Save
- 50대: Local Save
- 플랫폼 번호: 1=Android, 2=iOS, 3=PC(Steam)


---


## Primary References


- [01-policy](../01-policy/SKILL.md)
- [10-cloudsave-spec](../10-cloudsave-spec/SKILL.md)
- [20-cloudsave-client-api](../20-cloudsave-client-api/SKILL.md)


---


## Build / Defines


### Defines

- `DEV_CLOUDSAVE_GOOGLE` — Android Google Cloud Save 사용
- `DEV_CLOUDSAVE_ICLOUD` — iOS iCloud Cloud Save 사용
- `DEV_CLOUDSAVE_STEAM` — Steam Cloud Save 사용


### asmdef / Editor Runtime

- 플랫폼 SDK 의존 코드는 Runtime/Editor를 분리한다.
- 플랫폼별 코드는 define으로 컴파일 경로를 분기한다.


### Example Validation

UnityExample에서:
- 각 플랫폼 빌드에서 Cloud Save enable/disable 케이스를 쉽게 재현할 수 있어야 한다.


---


## Implementation Reference


### Cloud Save

| File | Description |
|------|-------------|
| `Samples~/CloudClientSystem/Runtime/CloudSave/CloudSaveResult.cs` | 결과 enum |
| `Samples~/CloudClientSystem/Runtime/CloudSave/CloudSavePayload.cs` | 데이터 클래스 |
| `Samples~/CloudClientSystem/Runtime/CloudSave/ICloudSaveClient.cs` | 플랫폼 클라이언트 인터페이스 |
| `Samples~/CloudClientSystem/Runtime/CloudSave/CloudSaveManager.cs` | 도구 레이어 매니저 |
| `Samples~/CloudClientSystem/Runtime/CloudSave/CloudSaveCrypto.cs` | SHA-256 체크섬 |
| `Samples~/CloudClientSystem/Runtime/CloudSave/GoogleCloudSaveClient.cs` | GPGS 구현 (Reflection) |
| `Samples~/CloudClientSystem/Runtime/CloudSave/FirebaseCloudSaveClient.cs` | Firebase(Firestore) 구현 |
| `Samples~/CloudClientSystem/Editor/CloudSave/CloudSaveManagerEditor.cs` | Inspector (Key/IV 생성) |


### Local Save

| File | Description |
|------|-------------|
| `Samples~/CloudClientSystem/Runtime/LocalSave/LocalSaveManager.cs` | 도구 레이어 매니저 |
| `Samples~/CloudClientSystem/Runtime/LocalSave/LocalSavePayload.cs` | 데이터 클래스 |
| `Samples~/CloudClientSystem/Runtime/LocalSave/LocalSaveCrypto.cs` | SHA-256 체크섬 |
| `Samples~/CloudClientSystem/Runtime/LocalSave/LocalSaveFileStore.cs` | 파일 I/O (atomic write) |
| `Samples~/CloudClientSystem/Runtime/LocalSave/Crypto.cs` | AES 암호화 유틸리티 |
| `Samples~/CloudClientSystem/Editor/LocalSave/LocalSaveManagerEditor.cs` | Inspector (Key/IV 생성) |


UPM root: `framework-cs/upm/com.devian.samples/`
Mirror: `framework-cs/apps/UnityExample/Packages/com.devian.samples/`
