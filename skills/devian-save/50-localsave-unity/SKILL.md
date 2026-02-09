# devian-save — Local Save (Unity Common)


Status: ACTIVE
AppliesTo: v10


---


## 1. Scope


Unity 로컬 파일 시스템 기반 Local Save 공통 규약.
스펙/정책은 [10-cloudsave-spec](../10-cloudsave-spec/SKILL.md)와 [01-policy](../01-policy/SKILL.md)를 따른다.


---


## 2. Responsibilities


- SavePayload 논리 필드 준수(버전/시간/payload/checksum) — Cloud Save와 **의미상 동일**
  - 직렬화 필드명/키는 구현에 따라 다를 수 있다(Cloud: PascalCase, Local: camelCase).
- SHA-256 checksum 필수
- 암호화/복호화는 Devian Crypto 유틸리티 사용
- 저장 경로/파일명 매핑을 외부 설정으로 제공
- Key/IV는 LocalSaveManager 내부에 저장
- Editor에서 Key/IV 자동 생성 기능 제공


### Encryption Key/IV


- `LocalSaveManager`는 AES key/IV를 **base64 문자열**로 취급한다.
- 내부 저장 필드는 `CString`이며, 실제 base64 문자열은 `CString.Value`로 사용된다.
- 키/IV의 저장/보관은 서비스 레이어 책임이며, 프레임워크는 `GetKeyIvBase64 / SetKeyIvBase64 / ClearKeyIv` 수단만 제공한다.


---


## 3. Path / File Naming


- 저장 경로는 **LocalSaveManager에서 설정** 가능해야 한다.
  - 예: `Application.persistentDataPath`, `Application.temporaryCachePath`
- 경로 선택은 enum 옵션으로 제공한다: `LocalSaveRoot`
  - `PersistentData`, `TemporaryCache`
- 파일명은 **List<slotKey, filename>** 구조로 사용자(개발자)가 설정한다.
- 슬롯 미설정 시 실패 처리(암묵적 기본값/하드코딩 금지).
- 파일명 유효성 검증: `IsValidJsonFilename`
  - 경로 구분자(`/`, `\`) 포함 금지
  - `..` 포함 금지 (traversal guard)
  - `.json` 확장자 필수


---


## 3.1. Payload Format


- `payload`는 **임의 문자열**이다.
- JSON은 권장일 뿐, **강제하지 않는다**.
- 압축 텍스트도 허용된다.


---


## 4. Runtime API (Unity)


### LocalSaveManager (CompoSingleton)

```csharp
public sealed class LocalSaveManager : CompoSingleton<LocalSaveManager>
{
    // Configuration
    public void Configure(LocalSaveRoot? root, bool? useEncryption, List<LocalSaveSlot> slots);

    // Sync API
    public CoreResult<bool> Save(string slot, string payload);
    public CoreResult<string> LoadPayload(string slot);

    // Async wrappers (Task.FromResult, no Task.Run)
    public Task<CoreResult<bool>> SaveAsync(string slot, string payload, CancellationToken ct);
    public Task<CoreResult<string>> LoadPayloadAsync(string slot, CancellationToken ct);

    // Key/IV export/import (no storage policy)
    public void GetKeyIvBase64(out string keyBase64, out string ivBase64);
    public CoreResult<bool> SetKeyIvBase64(string keyBase64, string ivBase64);
    public void ClearKeyIv();
}
```

- `SavePayload`는 내부에서 생성되며, 사용자가 직접 만들지 않는다.
- 버전은 내부 상수로 관리한다(사용자 설정 불가).
- `*Async` 메서드는 `Task.FromResult`로 동기 메서드를 감싸며, 스레드를 생성하지 않는다.


### Slot Mapping

- `List<LocalSaveSlot>`로 slotKey→filename 매핑을 구성한다.
- Runtime에서 `Configure()`로 덮어쓰기 가능(선택).


---


## 5. Editor


- LocalSaveManager Inspector에 **Generate Key/IV** 버튼을 제공한다.
- Key/IV는 Base64 문자열로 저장된다.
- `#if UNITY_EDITOR` 내 `GenerateKeyIv()` 메서드로 구현.


---


## 6. Save Pipeline (Order)


1. slot → filename 해석 (`TryResolveFilename`)
2. 파일명 유효성 검증 (`IsValidJsonFilename`)
3. encrypt(optional) using Devian `Crypto`
4. checksum(SHA-256) over ciphertext (`LocalSaveCrypto.ComputeSha256Base64`)
5. `LocalSavePayload` 생성 (SchemaVersion, UTC millis, cipher, checksum)
6. temp write + atomic rename (`LocalSaveFileStore.WriteAtomic`)


---


## 7. Load Pipeline


1. slot → filename 해석
2. 파일명 유효성 검증
3. read file (`LocalSaveFileStore.Read`)
4. checksum 검증(SHA-256)
5. decrypt(optional) using Devian `Crypto`
6. return plain payload


---


## 8. Failure Handling


- checksum 불일치: `CoreResult.Failure("localsave.checksum", ...)` 반환
- 파일 미존재: `CoreResult.Success(null)` 반환 (실패가 아님)
- 파일명 유효성 실패: `CoreResult.Failure("localsave.filename.invalid", ...)` 반환
- Key/IV 미설정: `CoreResult.Failure("localsave.keyiv", ...)` 반환

> fallback/재생성 정책은 **서비스 레이어 책임**이다.


---


## Implementation Reference


| Item | Path (UPM) |
|------|-----------|
| LocalSaveManager | `Runtime/Unity/LocalSave/LocalSaveManager.cs` |
| LocalSavePayload | `Runtime/Unity/LocalSave/LocalSavePayload.cs` |
| CloudSavePayload | `Runtime/Unity/CloudSave/CloudSavePayload.cs` *(논리 필드 비교용)* |
| LocalSaveCrypto | `Runtime/Unity/LocalSave/LocalSaveCrypto.cs` |
| LocalSaveFileStore | `Runtime/Unity/LocalSave/LocalSaveFileStore.cs` |
| Crypto | `Runtime/Unity/LocalSave/Crypto.cs` |
| LocalSaveManagerEditor | `Editor/LocalSave/LocalSaveManagerEditor.cs` |


UPM root: `framework-cs/upm/com.devian.foundation/`
Mirror: `framework-cs/apps/UnityExample/Packages/com.devian.foundation/`
