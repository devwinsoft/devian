# Save System


Common Android/iOS entry for Devian CloudSave.


- Android: Google Play Games Saved Games (GPGS)
- iOS: iCloud (Option A: NotAvailable stub)


This sample does **not** provide login/auth flow. The caller must ensure platform sign-in or service readiness as needed.




## Public API


- `ClaudSaveInstaller.InitializeAsync(CancellationToken ct)`
- `ClaudSaveInstaller.InitializeAsync(List<CloudSaveSlot> slots, bool useEncryption, CancellationToken ct)`




## Usage (recommended)


1) Put `CloudSaveManager` component in a scene (so `CloudSaveManager.Instance` exists).


2) (Optional) Configure slots/encryption via Inspector, or pass them via installer overload.


3) Call:


```csharp
await ClaudSaveInstaller.InitializeAsync(ct);
```

or:

```csharp
await ClaudSaveInstaller.InitializeAsync(slots, useEncryption: true, ct);
```




## Notes
- On iOS, this is currently a stub client (returns `CloudSaveResult.NotAvailable`). Real iCloud implementation is deferred.
- On Android, the default flow uses Devian `GoogleCloudSaveClient` (reflection-based).
