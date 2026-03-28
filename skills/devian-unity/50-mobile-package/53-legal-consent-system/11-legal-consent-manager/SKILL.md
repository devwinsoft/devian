# 11-legal-consent-manager

## Purpose

`LegalConsentManager`는 Legal/Consent 동의 상태의 단일 런타임 진입점이다.
현재 공용 설정 버전과 저장된 전체 동의 버전을 비교해서 동의 필요 여부를 판단하고, 동의 완료 버전/시각을 로컬에 저장한다.

## Code Path

- `framework-cs/apps/UnityExample/Assets/Samples/Devian Foundation/0.1.0/MobilePackage/Runtime/LegalConsent/LegalConsentManager.cs`
- `framework-cs/apps/UnityExample/Assets/Samples/Devian Foundation/0.1.0/MobilePackage/Runtime/LegalConsent/LegalConsentStorage.cs`

## Bootstrap

- `MobileApplication`에 `RequireComponent(typeof(LegalConsentManager))`가 선언되어 있다.
- `Assets/Resources/Devian/Application.prefab`에 `LegalConsentManager`가 부착되어 있다.
- `LegalConsentManager`는 `CompoSingleton<LegalConsentManager>` 패턴을 사용한다.

## Public API

- `Settings`
- `Storage`
- `GetDocuments()`
- `TryGetDocument(LegalDocumentType, out LegalDocumentConfig)`
- `NeedsConsent()`
- `GetDocumentUrl(LegalDocumentType)`
- `DownloadDocumentAsync(LegalDocumentType, CancellationToken)`
- `GetAcceptedVersion()`
- `GetAcceptedAtUtcMs()`
- `AcceptCurrentVersion(long acceptedAtUtcMs = 0L)`
- `ClearStorage()`

## Persistence

- `JsonPrefs<LegalConsentStorage>`를 사용한다.
- PlayerPrefs key: `Devian.LegalConsent.Storage`

## Storage Shape

`LegalConsentStorage`
- `schemaVersion`
- `isAccepted`
- `acceptedVersion : VersionNumber`
- `acceptedAtUtcMs`

## Decision Rules

- 전체 동의 기록이 없으면 `NeedsConsent()`는 `true`
- 저장 버전과 현재 설정 버전이 다르면 `NeedsConsent()`는 `true`
- `AcceptCurrentVersion()`는 현재 공용 설정 버전과 시각을 전체 동의 상태로 저장한다
- `GetDocumentUrl()`은 `{cdn base url}/{version}/{language}/{filename}` 규칙으로 URL을 조합한다
- `DownloadDocumentAsync()`는 호출 시점마다 CDN에서 문서를 내려받아 `string`으로 반환하고, 문서 본문을 캐싱/저장하지 않는다
- `language`는 `Application.systemLanguage`를 우선 사용하고, 미지원 언어는 `MobileApplication.DefaultLanguage`로 fallback 한다
- 언어 코드는 `Devian.Domain.Common.CommonUtils`를 사용해 변환한다
