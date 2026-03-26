# 53-legal-consent-system — Overview

Status: ACTIVE
AppliesTo: v10

MobilePackage 샘플의 Legal/Consent 최소 상태 시스템 개요다.
`LegalConsentManager`가 공용 문서 버전과 저장된 전체 동의 버전을 비교하고, `LegalConsentSettings.asset`이 문서 타입/파일명과 CDN base URL을 제공한다.

이 스킬 그룹 책임:
- 이용약관 / 개인정보처리방침 설정
- 전체 동의 필요 여부 판단
- 동의 완료 버전 / 시각 로컬 저장
- UI 화면과 분리된 런타임 API 제공

문서 중복 방지 라우팅:
- settings asset shape/path는 `10-legal-consent-settings`
- manager API / storage / persistence는 `11-legal-consent-manager`

---

## Start Here

| Document | Description |
|----------|-------------|
| [10-legal-consent-settings](../10-legal-consent-settings/SKILL.md) | LegalConsentSettings / LegalDocumentConfig |
| [11-legal-consent-manager](../11-legal-consent-manager/SKILL.md) | LegalConsentManager / LegalConsentStorage |

---

## Runtime Shape

- `LegalConsentSettings`
- `LegalDocumentConfig`
- `LegalConsentManager`
- `LegalConsentStorage`

---

## Related

- [11-mobile-application](../../11-mobile-application/SKILL.md)
- [21-savedata-system](../../21-savedata-system/00-overview/SKILL.md)
