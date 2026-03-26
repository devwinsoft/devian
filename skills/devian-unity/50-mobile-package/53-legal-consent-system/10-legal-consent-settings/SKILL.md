# 10-legal-consent-settings

## Purpose

`LegalConsentSettings`는 Legal/Consent 문서 설정 정본이다.
공용 버전, CDN base URL, 문서 타입, 파일명을 `ScriptableObject`로 보관한다.

## Code Path

- `framework-cs/apps/UnityExample/Assets/Samples/Devian Foundation/0.1.0/MobilePackage/Runtime/LegalConsent/LegalConsentSettings.cs`
- `framework-cs/apps/UnityExample/Assets/Samples/Devian Foundation/0.1.0/MobilePackage/Editor/LegalConsentSettingsEditor.cs`

## Asset Path

- `framework-cs/apps/UnityExample/Assets/Resources/Devian/LegalConsentSettings.asset`

## Constants

- `ResourcesPath = "Devian/LegalConsentSettings"`
- `DefaultResourcesAssetPath = "Assets/Resources/Devian/LegalConsentSettings.asset"`

## Shape

`LegalConsentSettings`
- `_version : VersionNumber`
- `_cdnBaseUrl : string`
- `_documents : LegalDocumentConfig[]`

`LegalDocumentConfig`
- `_documentType : LegalDocumentType`
- `_filename : string`

## Document Types

- `TermsOfService`
- `PrivacyPolicy`

## Notes

- 문서 본문은 앱에 포함하지 않는다.
- 문서 버전은 문서별이 아니라 `LegalConsentSettings._version` 공용 필드다.
- 문서 URL은 설정값으로 직접 저장하지 않는다.
- 문서 URL은 `{cdn base url}/{version}/{language}/{filename}` 규칙으로 조합한다.
- `language`는 Unity `Application.systemLanguage`를 사용하고, 미지원 언어는 `MobileApplication.DefaultLanguage`로 fallback 한다.
- 모든 문서는 필수로 간주한다.
- Inspector 하단에는 Korean URL example이 출력되며, 각 문서 URL을 바로 열어 확인할 수 있다.
