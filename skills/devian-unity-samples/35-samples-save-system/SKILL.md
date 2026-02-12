# devian-unity-samples — Save System (Common Android/iOS Sample)




## Scope
- Android(GPGS) / iOS(Firebase 임시) 공통 엔트리로 Devian `CloudSaveManager`를 초기화하는 샘플 진입점을 제공한다.
- 내부에서 플랫폼 분기(Android=GPGS, iOS=FirebaseCloudSaveClient 임시)를 수행한다. iCloud(AppleCloudSaveClient)는 설계대로 유지하되, 준비 완료 후 교체한다.




## What this sample does / does not
- Does:
  - `ClaudSaveInstaller.InitializeAsync(ct)`로 플랫폼별 client 선택 후
  - `CloudSaveManager.Instance.InitializeAsync(ct)`를 호출한다.
- Does not:
  - 로그인/인증 플로우(사인인), 트리거/주기, 재시도/충돌 정책 같은 비즈니스 로직
- iOS: `FirebaseCloudSaveClient`를 주입해 `CloudSaveManager`를 초기화(임시)
- iCloud(`AppleCloudSaveClient`)는 유지(현재 미사용)




## Assembly Definition (asmdef)
- Runtime asmdef:
  - UPM: `framework-cs/upm/com.devian.samples/Samples~/SaveSystem/Runtime/Devian.Samples.ClaudSave.asmdef`
  - UnityExample mirror: `framework-cs/apps/UnityExample/Packages/com.devian.samples/Samples~/SaveSystem/Runtime/Devian.Samples.ClaudSave.asmdef`
- Editor asmdef:
  - UPM: `framework-cs/upm/com.devian.samples/Samples~/SaveSystem/Editor/Devian.Samples.ClaudSave.Editor.asmdef`
  - UnityExample mirror: `framework-cs/apps/UnityExample/Packages/com.devian.samples/Samples~/SaveSystem/Editor/Devian.Samples.ClaudSave.Editor.asmdef`




## Locations (mirrored)
- UPM:
  - `framework-cs/upm/com.devian.samples/Samples~/SaveSystem/README.md`
  - `framework-cs/upm/com.devian.samples/Samples~/SaveSystem/Runtime/ClaudSaveInstaller.cs`
  - `framework-cs/upm/com.devian.samples/Samples~/SaveSystem/Runtime/AppleCloudSaveClient.cs`
  - `framework-cs/upm/com.devian.samples/Samples~/SaveSystem/Runtime/CloudSave/` — CloudSaveManager, ICloudSaveClient, GoogleCloudSaveClient 등 6개
  - `framework-cs/upm/com.devian.samples/Samples~/SaveSystem/Runtime/LocalSave/` — LocalSaveManager, LocalSaveCrypto 등 5개
  - `framework-cs/upm/com.devian.samples/Samples~/SaveSystem/Editor/CloudSave/CloudSaveManagerEditor.cs`
  - `framework-cs/upm/com.devian.samples/Samples~/SaveSystem/Editor/LocalSave/LocalSaveManagerEditor.cs`
- UnityExample mirror (직접 수정 금지):
  - `framework-cs/apps/UnityExample/Packages/com.devian.samples/Samples~/SaveSystem/...`




## UPM Samples UI 등록(근거)
- `framework-cs/upm/com.devian.samples/package.json`
  - `samples[].path == "Samples~/SaveSystem"`
  - `displayName == "Save System"`




## Public API (runtime)
- `ClaudSaveInstaller.InitializeAsync(CancellationToken ct)` — 공통 엔트리(내부 플랫폼 분기)
- `ClaudSaveInstaller.InitializeAsync(List<CloudSaveSlot> slots, bool useEncryption, CancellationToken ct)` — slots/encryption 포함 오버로드




## Usage (recommended)
1. 씬에 `CloudSaveManager` 컴포넌트를 배치해서 `CloudSaveManager.Instance`가 존재하도록 한다.
2. (선택) `CloudSaveManager` Inspector에서 slots/encryption/Key/IV를 설정하거나, installer 오버로드로 전달한다.
3. 호출:


```csharp
await ClaudSaveInstaller.InitializeAsync(ct);
```




## Notes
- iOS에서는 현재 `FirebaseCloudSaveClient`가 주입되어 동작한다(임시).
- `AppleCloudSaveClient`(iCloud)는 설계대로 유지하며, iCloud 구현 준비 완료 후 iOS 주입을 교체한다.
- Android에서는 `GoogleCloudSaveClient`(GPGS)가 기본 선택된다.
- [20-save-system — 25-cloudsave-firebase](../../devian-unity/20-save-system/25-cloudsave-firebase/SKILL.md)
