# CloudSave (GPGS) Sample


This sample demonstrates how to initialize Devian Cloud Save for GPGS by **injecting only the client**
from a dedicated integration point (samples).


- Slots/encryption/Key/IV are configured on `CloudSaveManager` (Inspector / serialized fields).
- This sample does **not** implement business logic (triggers/cycles/retry/conflict policy).
- The actual Google Play Games Services (Snapshots) implementation is intentionally left to the developer.
- Foundation remains SDK-free. This lives in `com.devian.samples`.


## What you get


- `GpgsCloudSaveInstaller.ConfigureCloudSave(object googlePlayService)`
  - Creates a client and calls `CloudSaveManager.Instance.Configure(client: ...)` only.
  - Uses `CloudSaveManager` serialized settings for slots/encryption.


- `GpgsCloudSaveInstaller.ConfigureCloudSave(object googlePlayService, List<CloudSaveSlot> slots, bool useEncryption)`
  - Legacy convenience overload (kept for compatibility).


- `GpgsCloudSaveClient : ICloudSaveClient`
  - Samples-only stub that you can implement with GPGS Snapshots.


## How to initialize (recommended)


1. Put `CloudSaveManager` component in a scene (so `CloudSaveManager.Instance` exists).
2. Configure slots/encryption/Key/IV on `CloudSaveManager` Inspector.
3. After your GooglePlayService is ready (and sign-in flow is handled by your app), call:


```csharp
GpgsCloudSaveInstaller.ConfigureCloudSave(googlePlayService);
```


## Notes


- This sample avoids hard dependency on any specific GPGS SDK types so it can compile as a sample without SDK.
  Replace `object googlePlayService` with your project type if you want tighter integration.
