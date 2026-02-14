# 41-samples-localsave-manager — LocalSave Manager (Sample)


## Purpose
- 로컬 파일 기반 저장/불러오기를 제공하는 샘플 매니저.
- AES 암호화, SHA-256 체크섬, atomic write를 지원한다.


## Locations (mirrored)
- UPM:
  - `framework-cs/upm/com.devian.samples/Samples~/MobileSystem/Runtime/LocalSave/LocalSaveManager.cs`
  - `framework-cs/upm/com.devian.samples/Samples~/MobileSystem/Runtime/LocalSave/LocalSavePayload.cs`
  - `framework-cs/upm/com.devian.samples/Samples~/MobileSystem/Runtime/LocalSave/LocalSaveCrypto.cs`
  - `framework-cs/upm/com.devian.samples/Samples~/MobileSystem/Runtime/LocalSave/LocalSaveFileStore.cs`
  - `framework-cs/module/Devian/src/Core/Crypto.cs`
  - `framework-cs/upm/com.devian.samples/Samples~/MobileSystem/Editor/LocalSave/LocalSaveManagerEditor.cs`
- UnityExample mirror (직접 수정 금지):
  - `framework-cs/apps/UnityExample/Packages/com.devian.samples/Samples~/MobileSystem/...`


## Assembly Definition (asmdef)
- 단일 asmdef(`Devian.Samples.CloudClientSystem`)에 포함되어 MobileSystem 번들 샘플과 함께 설치된다.


## What it does
- `LocalSaveManager`가 파일 I/O를 수행한다(slot 단위 저장/불러오기).
- `LocalSaveCrypto`로 SHA-256 체크섬을 생성/검증한다.
- `Crypto`로 AES 암호화/복호화를 수행한다.
- `LocalSaveFileStore`가 atomic write(임시 파일 → rename)로 파일을 안전하게 기록한다.
- `LocalSaveManagerEditor`가 Inspector에서 Key/IV 생성을 지원한다.


## Non-goals
- Cloud Save(서버 저장)는 이 스킬 범위 밖이다. → [42-samples-cloudsave-manager](../42-samples-cloudsave-manager/SKILL.md)


## Links
- [30-samples-mobile-system](../30-samples-mobile-system/SKILL.md)
- [42-samples-cloudsave-manager](../42-samples-cloudsave-manager/SKILL.md)
