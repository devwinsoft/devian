# 20-common-utils

Status: ACTIVE
AppliesTo: v10

## Purpose

`CommonUtils`은 Common Domain의 공용 헬퍼다.
현재 구현 범위에서는 Unity `SystemLanguage.ToString()` 값과 같은 언어 이름 문자열을 URL/리소스 경로용 언어 코드로 변환한다.

## Code Path

- C#: `framework-cs/module/Devian.Domain.Common/src/CommonUtils.cs`
- Unity Sample Mirror: `framework-cs/apps/UnityExample/Assets/Samples/Devian Foundation/0.1.0/CommonPackage/Runtime/Module/CommonUtils.cs`
- TS: `framework-ts/module/devian-domain-common/features/commonUtils.ts`

## Public API

### C#

- `CommonUtils.TryGetLanguageCode(string language, out string code)`
- `CommonUtils.GetLanguageCodeOrEmpty(string language)`

### TypeScript

- `tryGetLanguageCode(language: string): string | null`
- `getLanguageCodeOrEmpty(language: string): string`

## Mapping

- `Korean -> ko`
- `English -> en`
- `Japanese -> ja`
- `ChineseSimplified -> zh-Hans`
- `ChineseTraditional -> zh-Hant`
- `German -> de`
- `French -> fr`
- `Spanish -> es`
- `Portuguese -> pt`
- `Russian -> ru`
- `Thai -> th`
- `Vietnamese -> vi`
- `Indonesian -> id`
- 미지원 언어는 빈 문자열 또는 `null`

## Usage

- `LegalConsentManager`는 문서 URL의 `{language}` segment를 만들 때 `CommonUtils`를 사용한다.
- `LegalConsentManager`는 `Application.systemLanguage.ToString()`와 `DefaultLanguage.ToString()` 값을 `CommonUtils`에 전달한다.
- fallback 결정은 `CommonUtils`이 아니라 호출부가 담당한다.
