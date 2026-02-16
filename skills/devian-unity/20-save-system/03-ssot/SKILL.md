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


## Security / Portability Policy

### Anti-Tamper Scope

- 변조/치트 방지는 **Save payload를 해석한 이후의 게임 로직**에서 처리한다.
- Save 시스템 범위에서는 **암호화(Confidentiality)** 만 책임진다.

### Local Save — Device-Bound Encryption

- Local Save는 **기기 바인딩 키 기반 암호화**를 전제로 한다.
  - 목표: Local 저장 파일만 복사해서 다른 기기에서 사용하는 것을 방지한다.
- 구현 시 플랫폼 보안 저장소를 사용한다.
  - Android: Keystore
  - iOS: Keychain
- Editor 환경은 플랫폼 보안 저장소가 없으므로, Editor 전용 정책/테스트 경로를 사용할 수 있다.

#### Local / Cloud 키 분리 (Hard)

- **Local Save**와 **Cloud Save**는 서로 다른 키 소스를 사용해야 한다.
  - Local: 기기 바인딩 키(KEK/DEK) — 해당 기기에서만 복호화 가능
  - Cloud: 계정 공유 키(SaveSlotConfig.keyBase64/ivBase64) — 다른 기기에서도 복호화 가능
- 구현: `SaveDataManager` 내부에 키 획득 경로를 분리한다.
  - `tryGetLocalKeyIv(...)` → 기기 바인딩 키(KEK/DEK)
  - `tryGetCloudKeyIv(...)` → 기존 `_slotConfig.TryGetKeyIv(...)` 재사용
- 호출 매핑:
  - `saveLocal`, `loadLocalRecord` → `tryGetLocalKeyIv`
  - `saveCloudInternal`, `loadCloudRecordInternal` → `tryGetCloudKeyIv`
- **위반 시 문제**: Cloud까지 기기 바인딩되면 다른 기기에서 클라우드 데이터를 복호화할 수 없어, 계정 이관/멀티디바이스가 깨진다.

#### Payload Contract — Ciphertext-only (Hard)

- `SaveLocalPayload.payload` 와 `SaveCloudPayload.Payload` 는 **항상 저장 포맷 그대로**를 의미한다.
  - `_slotConfig.useEncryption == true` 인 경우: **항상 암호문(ciphertext)** 이다.
  - `_slotConfig.useEncryption == false` 인 경우: 평문 JSON 문자열을 허용한다.
- **금지:** SaveDataManager가 로드 결과를 반환할 때 payload 필드에 **복호화된 JSON(plaintext)** 을 넣어 "의미를 바꿔치기" 하는 것.
  - 로드 결과 payload는 **저장된 payload 그대로**(cipher/plain)를 유지해야 한다.

#### Decrypt → Parse Rule (Hard)

- `JsonUtility.FromJson<T>()` 는 **반드시 복호화된 plaintext JSON** 에만 호출한다.
- 따라서 `Payload → 바로 Parse` 는 금지한다.
- 파싱 순서는 아래로 고정한다:
  1) Source-aware Decrypt (Local: device-bound / Cloud: shared key)
  2) Parse json (`JsonUtility.FromJson<T>`)
- Source-aware Decrypt는 키 정책이 필요하므로 **SaveDataManager가 책임**진다.

#### Cross-Save Rule — Decrypt→Save (Hard)

- Local ↔ Cloud 간 "교차 저장(cross-save)"은 **반드시** 아래 순서를 따른다:
  1) Source-aware Decrypt (Local: device-bound key / Cloud: shared key)
  2) plaintext JSON
  3) Target Save (Local/Cloud 저장 시 각각의 키로 1회 암호화)
- **금지:** ciphertext payload 문자열을 그대로 `saveLocal*` / `saveCloud*` 에 전달하는 것.
  - (이유) 로드가 ciphertext를 반환(선택지2 계약)하는 상태에서, 저장 함수는 plaintext를 입력으로 암호화를 수행하므로 double-encryption이 발생한다.

#### Slot Sync Overload (Hard)

- `SaveDataManager.SyncAsync(CancellationToken)` 는 전체 슬롯을 대상으로 동작하며, 성공 시 payload를 반환하지 않을 수 있다.
- 특정 슬롯의 payload가 필요한 경우 `SaveDataManager.SyncAsync(string slot, CancellationToken)` 오버로드를 사용한다.
  - Guest/Editor(Local-only)에서도 해당 slot의 `LocalPayload`를 로드하여 `SyncResult.LocalPayload`를 채워 반환한다.
  - Cloud 사용 로그인에서도 slot 1개만 대상으로 sync를 수행하며, 가능한 경우 `LocalPayload/CloudPayload`를 채워 반환한다.

#### Editor/Guest Local-only Merge (Hard)

- Guest Login과 Editor Login은 저장/동기화 관점에서 동일하게 **Local-only** 로 취급한다.
  - `UNITY_EDITOR` 빌드에서는 항상 Local-only로 동작한다.
- Local-only 판정 및 "로컬 데이터 존재 여부 검사"는 중복 구현을 금지하고, `SaveDataManager` 내부 helper로 통일한다.
  - `isLocalOnly(LoginType)`
  - `hasAnyLocalAsync(CancellationToken)`

#### Guest Local Encryption

Guest Login(Firebase Anonymous) 상태에서도 Local Save 암호화는 **기기 바인딩(Device-bound)** 을 만족해야 한다.

##### Key Model — DEK / KEK (Hard)

| 키 | 역할 | 알고리즘 | 저장 위치 |
|-----|------|---------|----------|
| **DEK** (Data Encryption Key) | Local payload를 직접 암호화/복호화 | AES 대칭키 | 디스크에 **wrapped DEK** 형태로만 저장 (평문 저장 금지) |
| **KEK** (Key Encryption Key) | DEK를 래핑(wrap)/언래핑(unwrap) | 플랫폼 보안 저장소가 관리 | Android Keystore / iOS Keychain |

##### KEK — Non-Exportable Requirement (Hard)

- KEK는 반드시 OS 보안 저장소 내부에서 **생성 및 보관**되어야 한다.
  - Android: Keystore (`setIsStrongBoxBacked` 또는 TEE)
  - iOS: Keychain (`kSecAttrAccessibleWhenUnlockedThisDeviceOnly`)
- KEK의 key material은 **non-exportable(추출 불가)** 이어야 한다.
  - 앱 코드가 KEK 원본 바이트를 읽거나, 직렬화하거나, 다른 저장소로 복사하는 것은 금지된다.
  - 래핑/언래핑 연산은 보안 저장소 API를 통해서만 수행한다.

##### Platform Notes

- Android (Keystore)
  - KEK는 Android Keystore에 생성/보관한다.
  - KEK는 non-exportable이어야 하며, 앱이 key material을 바이트 배열로 추출할 수 없어야 한다.
- iOS (Keychain)
  - KEK(또는 KEK에 준하는 보호)는 iOS Keychain을 사용한다.
  - Keychain 저장 항목은 앱/번들 스코프에 묶이며, "파일만 복사"로는 복호화가 불가능해야 한다.
- Editor
  - 플랫폼 보안 저장소가 없으므로 Editor 전용 테스트 경로를 허용한다.

##### Wrapped DEK — Storage Rule (Hard)

- DEK를 로컬 파일이나 SharedPreferences/UserDefaults 등에 **평문으로 저장하는 것은 금지**한다.
- 디스크에 기록할 수 있는 유일한 형태는 **KEK로 래핑된 wrapped DEK** 이다.
- 앱 실행 시 wrapped DEK를 보안 저장소 API로 언래핑하여 메모리상 DEK를 획득한 뒤 payload를 복호화한다.

##### Copy-Protection Property (Hard)

- 로컬 저장 파일(payload + wrapped DEK)을 다른 기기로 복사해도, 대상 기기에는 **원본 KEK가 존재하지 않는다**.
- 따라서 wrapped DEK를 언래핑할 수 없으므로 **payload 복호화는 반드시 실패**해야 한다.
- 이 성질을 통해 "로컬 파일 복사만으로 타 기기에서 세이브 데이터를 사용하는 것"을 방지한다.

##### Guest Migration (Hard)

- Guest → 계정 연결(구글/애플) 이관은 **"파일 복사"가 아니다**.
- 이관은 반드시 **같은 기기(KEK가 존재하는 기기)** 에서만 수행한다:
  1. 같은 기기에서 wrapped DEK를 KEK로 **언래핑** → DEK 획득
  2. DEK로 Guest 로컬 payload를 **복호화**
  3. 복호화된 데이터를 계정 기준 저장소(Cloud/Firestore)로 **업로드/귀속**
- 다른 기기로 파일을 옮긴 뒤 이관하는 경로는 제공하지 않는다.

##### DEK Material Spec (Hard)

- DEK는 **key(32바이트) + iv(16바이트) = 48바이트**를 하나의 비밀 단위로 관리한다.
- 이 48바이트 전체가 KEK로 래핑되어 wrapped DEK blob을 형성한다.
- 앱 실행 시 wrapped DEK를 KEK로 언래핑하면 48바이트를 복원하고, 그 중 key/iv를 분리하여 AES 암복호화에 사용한다.

##### Platform Implementation Policy (Hard)

**Android (Guest / GPGS 공통)**
- Keystore에 **AES-GCM non-exportable 키**를 KEK로 생성한다.
- DEK 48바이트를 KEK(AES-GCM)로 암호화하여 wrapped blob을 생성한다.
- 언래핑: Keystore API로 wrapped blob을 복호화 → DEK 48바이트 복원.

**iOS (Guest / Apple 공통)**
- Keychain에 DEK 48바이트를 **`kSecAttrAccessibleWhenUnlockedThisDeviceOnly`** 속성으로 저장한다.
- 이 방식은 Keychain이 DEK 자체를 기기 바인딩으로 보호하는 형태이다.
- 별도 KEK를 두고 wrap 구조로 구현해도 허용하나, 최소 구현은 Keychain 직접 저장이다.

**Editor**
- 플랫폼 보안 저장소가 없으므로 기기 바인딩 보안은 목표가 아니다.
- 기존 `SaveSlotConfig.keyBase64/ivBase64`(테스트 키)를 그대로 사용한다.

##### Wrapped DEK Storage Location

- wrapped DEK blob은 기기 밖으로 나가면 무의미하므로 로컬 저장 허용:
  - **권장**: `PlayerPrefs`에 Base64 문자열로 저장
  - **대안**: `persistentDataPath`에 바이너리 파일로 저장
- 앱 삭제 시 wrapped DEK가 함께 소실될 수 있으나, **Data Loss Policy(복구 책임 없음)** 에 따라 허용.

##### Legacy Migration — Fallback Decode (Hard)

- 기기 바인딩 키 도입 이전에 `SaveSlotConfig.keyBase64/ivBase64`로 암호화된 기존 Local 파일이 존재할 수 있다.
- 로드 시 다음 순서를 따른다:
  1. **기기 바인딩 키(DEK/KEK)** 로 복호화 시도
  2. 실패 시 **레거시 키(`SaveSlotConfig.keyBase64/ivBase64`)** 로 복호화 재시도
  3. 레거시 키로 성공 시, 즉시 **기기 바인딩 키로 리암호화(re-encrypt)** 하여 저장 — 1회성 마이그레이션
- 레거시 폴백은 **로드 경로에서만** 적용한다. 저장 시에는 항상 기기 바인딩 키를 사용한다.
- 레거시 키 폴백 복호화는 **같은 기기에서만 허용**한다.
  - 조건: `SaveLocalPayload.deviceId == currentDeviceId`
  - 불일치 시 레거시 폴백 복호화는 차단하여, "로컬 파일 복사로 타 기기에서 1회 실행" 구멍을 방지한다.

### Data Loss Policy (Local)

- 앱 삭제/초기화 등으로 Local 데이터가 손실되는 경우, **복구/지원 책임 범위가 아니다.**

### Guest Login Migration

- Guest Login은 Firebase Anonymous(익명 로그인)를 사용한다.
- Guest 상태에서의 이관은 "파일 복사"가 아니라, **계정 연결(구글/애플 로그인) 시점의 공식 이관 경로**로만 제공한다.
- 이관의 최소 보장 범위:
  - **구매 내역**
  - **게임 데이터**
- 구매 내역은 Local에 의존하지 않으며, 서버/Firestore 등 **계정 기준 저장**을 유지한다.

### Login State Behavior Matrix

| 로그인 상태 | Local Save 키 | Cloud Save 키 | 복사 방지 | 이관 경로 |
|-------------|--------------|--------------|----------|----------|
| **Guest** (Firebase Anonymous) | 기기 바인딩(KEK/DEK) | N/A (Cloud 미사용) | O — 파일 복사 시 복호화 실패 | 같은 기기에서 계정 연결 후 Cloud 업로드 |
| **AOS** (GPGS) | 기기 바인딩(KEK/DEK) | 계정 공유 키(SaveSlotConfig) | Local: O / Cloud: X (다른 기기 복호화 가능) | Cloud 기반 멀티디바이스 |
| **iOS** (Apple) | 기기 바인딩(KEK/DEK) | 계정 공유 키(SaveSlotConfig) | Local: O / Cloud: X (다른 기기 복호화 가능) | Cloud 기반 멀티디바이스 |

- **O** = 복사 방지 적용 (타 기기 복호화 실패)
- **X** = 복사 방지 미적용 (정상 복호화 가능 — 의도된 동작)

### Acceptance Criteria (Device-Bound Encryption)

다음 시나리오가 모두 통과해야 기기 바인딩 암호화 구현이 완료된 것으로 간주한다.

| # | 시나리오 | 기대 결과 |
|---|---------|----------|
| AC-1 | 다른 기기로 Local 파일(payload + wrapped DEK)만 복사 후 로드 | **복호화 실패** (KEK 부재) |
| AC-2 | 같은 기기에서 Guest로 저장 → 같은 기기에서 로드 | **정상 로드** |
| AC-3 | Guest → Google/Apple 계정 연결 → Cloud 업로드 → 다른 기기에서 로그인 후 Cloud 로드 | **정상 로드** (Cloud는 계정 공유 키) |
| AC-4 | 앱 삭제 후 재설치 → Local 로드 시도 | **로드 실패** (Data Loss Policy 허용) |
| AC-5 | 레거시(keyBase64 기반) Local 파일이 있는 상태에서 업데이트 후 로드 | **레거시 키로 폴백 복호화 → 기기 바인딩 키로 리암호화 저장** |
| AC-6 | AOS/iOS 계정 로그인 상태에서 Local 저장 후, 같은 계정으로 다른 기기에서 Cloud 로드 | **Cloud 정상 로드** (Local/Cloud 키 분리 확인) |


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
| `Samples~/MobileSystem/Runtime/SaveData/SaveCloud/SaveCloudResult.cs` | 결과 enum |
| `Samples~/MobileSystem/Runtime/SaveData/SaveCloud/SaveCloudPayload.cs` | 데이터 클래스 |
| `Samples~/MobileSystem/Runtime/SaveData/SaveCloud/ISaveCloudClient.cs` | 플랫폼 클라이언트 인터페이스 |
| `Samples~/MobileSystem/Runtime/SaveData/SaveCloud/SaveCloudManager.cs` | 도구 레이어 매니저 |
| `Samples~/MobileSystem/Runtime/SaveData/SaveCloud/SaveCloudCrypto.cs` | SHA-256 체크섬 |
| `Samples~/MobileSystem/Runtime/SaveData/SaveCloud/SaveCloudClientGoogle.cs` | GPGS 구현 (Reflection) |
| `Samples~/MobileSystem/Runtime/SaveData/SaveCloud/SaveCloudClient.cs` | Firebase(Firestore) 구현 |
| `Samples~/MobileSystem/Editor/SaveCloud/SaveCloudManagerEditor.cs` | Inspector (Key/IV 생성) |


### Local Save

| File | Description |
|------|-------------|
| `Samples~/MobileSystem/Runtime/SaveData/SaveLocal/SaveLocalManager.cs` | 도구 레이어 매니저 |
| `Samples~/MobileSystem/Runtime/SaveData/SaveLocal/SaveLocalPayload.cs` | 데이터 클래스 |
| `Samples~/MobileSystem/Runtime/SaveData/SaveLocal/SaveLocalCrypto.cs` | SHA-256 체크섬 |
| `Samples~/MobileSystem/Runtime/SaveData/SaveLocal/SaveLocalFileStore.cs` | 파일 I/O (atomic write) |
| `framework-cs/module/Devian/src/Core/Crypto.cs` | AES 암호화 유틸리티 |
| `Samples~/MobileSystem/Editor/SaveLocal/SaveLocalManagerEditor.cs` | Inspector (Key/IV 생성) |


UPM root: `framework-cs/upm/com.devian.samples/`
Mirror: `framework-cs/apps/UnityExample/Packages/com.devian.samples/`
