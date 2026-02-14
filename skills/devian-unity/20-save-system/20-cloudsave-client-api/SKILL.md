# 20-save-system — Cloud Save Client API


Status: ACTIVE
AppliesTo: v10


---


## 1. CloudSaveClientApple Interface (Contract)


플랫폼별 구현은 `CloudSaveClientApple` 인터페이스를 구현해야 한다.


```csharp
public interface CloudSaveClientApple
{
    bool IsAvailable { get; }
    Task<CloudSaveResult> SignInIfNeededAsync(CancellationToken ct);
    Task<(CloudSaveResult result, CloudSavePayload payload)> LoadAsync(string slot, CancellationToken ct);
    Task<CloudSaveResult> SaveAsync(string slot, CloudSavePayload payload, CancellationToken ct);
    Task<CloudSaveResult> DeleteAsync(string slot, CancellationToken ct);
}
```


### CloudSaveResult

```csharp
public enum CloudSaveResult
{
    Success = 0,
    NotFound = 1,
    NotAvailable = 10,
    AuthRequired = 11,
    TemporaryFailure = 20,
    FatalFailure = 30,
}
```


---


## 2. CloudSaveManager Public API (Contract)


`CloudSaveManager`는 `CloudSaveClientApple`를 감싸는 도구 레이어다.

```csharp
public sealed class CloudSaveManager : CompoSingleton<CloudSaveManager>
{
    public void Configure(CloudSaveClientApple client, bool? useEncryption, List<CloudSaveSlot> slots);
    public bool IsAvailable { get; }
    public Task<CoreResult<CloudSaveResult>> SignInIfNeededAsync(CancellationToken ct);
    public Task<CoreResult<bool>> SaveAsync(string slot, string payload, CancellationToken ct);
    public Task<CoreResult<string>> LoadPayloadAsync(string slot, CancellationToken ct);
    public Task<CoreResult<bool>> DeleteAsync(string slot, CancellationToken ct);
}
```

- 슬롯 allowlist 검증 (`TryResolveCloudSlot`)
- JSON 형식 검증 (`IsLikelyJson` — `{}` 또는 `[]` wrapping)
- 암호화/복호화 (선택, `Crypto.EncryptAes`/`DecryptAes`)
- 체크섬 (`CloudSaveCrypto.ComputeSha256Base64`)


---


## 3. Thread / Unity Rules


- Unity 메인 스레드 제약이 있는 플랫폼 SDK는 메인 스레드에서만 호출한다.
- 비동기 호출은 `CancellationToken`을 통해 취소를 지원한다.


---


## 4. Data Rules


- payload 버전 체크/마이그레이션은 클라이언트 상위 레이어에서 처리한다.
- 이 문서는 API 계약만 정의하며, UI 표시/문구는 비목표다.


---


## Implementation Reference


| Item | Path (UPM) |
|------|-----------|
| CloudSaveClientApple | `Runtime/Unity/CloudSave/CloudSaveClientApple.cs` |
| CloudSaveResult | `Runtime/Unity/CloudSave/CloudSaveResult.cs` |
| CloudSavePayload | `Runtime/Unity/CloudSave/CloudSavePayload.cs` |
| CloudSaveManager | `Runtime/Unity/CloudSave/CloudSaveManager.cs` |
| CloudSaveCrypto | `Runtime/Unity/CloudSave/CloudSaveCrypto.cs` |


UPM root: `framework-cs/upm/com.devian.foundation/`
