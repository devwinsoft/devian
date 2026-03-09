# Devian v10 — Overview

Status: ACTIVE
AppliesTo: v10
SSOT: skills/devian/10-module/03-ssot/SKILL.md

## What is Devian?

Devian은 **DATA**와 **PROTOCOL** 두 축으로 구성된 코드 생성 프레임워크다.

| 축 | 역할 | 입력 | 출력 |
|-----|------|------|------|
| **DATA** | 테이블/계약 타입 생성 | XLSX, JSON contracts | C#/TS Entity, Container, NDJSON/pb64 |
| **PROTOCOL** | 네트워크 메시지 생성 | Protocol JSON | C#/TS Message, Codec, Stub |

---

## Policy SSOT

모든 정책/경로/규칙의 정본은 **SSOT 문서**에 있다:

→ `skills/devian/10-module/03-ssot/SKILL.md`

이 문서를 포함한 모든 스킬은 SSOT를 참조하며, 충돌 시 SSOT가 우선한다.

---

## Devian Skill Groups

| Group | Description | Overview | Policy | SSOT |
|-------|-------------|----------|--------|------|
| **devian/10-module** | Root SSOT, 스킬 규격, 런타임 | [00-overview](../10-module/00-overview/SKILL.md) | [01-policy](../10-module/01-policy/SKILL.md) | [03-ssot](../10-module/03-ssot/SKILL.md) |
| **devian/20-domain-common** | Common Domain C#/TS 공통 정책 | [00-overview](../20-domain-common/00-overview/SKILL.md) | [01-policy](../20-domain-common/01-policy/SKILL.md) | — |
| **devian/80-tools** | CLI, 아카이브 | [00-overview](../80-tools/00-overview/SKILL.md) | [01-policy](../80-tools/01-policy/SKILL.md) | [03-ssot](../80-tools/03-ssot/SKILL.md) |
| **devian/80-tools/11-builder** | Build, Table, Contract, NDJSON, PB64, Protocol Codegen, Error Reporting | [00-overview](../80-tools/11-builder/00-overview/SKILL.md) | [01-policy](../80-tools/11-builder/01-policy/SKILL.md) | [03-ssot](../80-tools/11-builder/03-ssot/SKILL.md) |
| **devian-unity/20-domain-common-system** | Unity 공용 런타임 컴포넌트 | [00-overview](../../devian-unity/20-domain-common-system/00-overview/SKILL.md) | — | — |
| **devian-unity** | UPM 패키지, Unity 런타임, 컴포넌트 | [00-overview](../../devian-unity/00-overview/SKILL.md) | [01-policy](../../devian-unity/01-policy/SKILL.md) | [03-ssot](../../devian-unity/03-ssot/SKILL.md) |
| **devian-examples** | 예제 도메인, 예제 프로토콜 | [00-overview](../../devian-examples/00-overview/SKILL.md) | [01-policy](../../devian-examples/01-policy/SKILL.md) | [03-ssot](../../devian-examples/03-ssot/SKILL.md) |
| **devian-unity/21-domain-game-system** | Game Contents (Devian Samples) | [00-overview](../../devian-unity/21-domain-game-system/00-overview/SKILL.md) | [01-policy](../../devian-unity/21-domain-game-system/01-policy/SKILL.md) | — |
| **devian-unity/50-mobile-system** | MobileSystem (Devian Samples) | [00-overview](../../devian-unity/50-mobile-system/00-overview/SKILL.md) | [01-policy](../../devian-unity/50-mobile-system/01-policy/SKILL.md) | — |

---

## Routing (Central)

이 문서는 Devian 전체의 **단일 라우팅 정본**이다.
키워드/의도 기반으로 어디 스킬로 가야 하는지 여기서만 결정한다.

### Group Routing

- Unity 일반 컴포넌트(Non-UI) → `skills/devian-unity/10-foundation/00-overview/SKILL.md`
- Unity UI 컴포넌트(UI/Canvas/Frame/UIManager) → `skills/devian-unity/30-ui-system/00-overview/skill.md`

### Routing Keywords

| keyword | route-to |
|---|---|
| StringTable, StringTable.xlsx, string-table, ST_, 다국어, localization, localizing, TEXT table, 번역 | `skills/devian-unity/20-domain-common-system/30-string-table/SKILL.md` |
| ActorObject, ActorController, actor-system, actor controller | `skills/devian-unity/20-domain-common-system/10-actor-system/SKILL.md` |
| InputManager, input manager, InputActionAsset, input action asset | `skills/devian-unity/20-domain-common-system/22-input-manager/SKILL.md` |
| BaseInputController, input-controller, input controller, InputSpace | `skills/devian-unity/20-domain-common-system/21-input-controller/SKILL.md` |
| NDJSON, ndjson, .json 스토리지 | `skills/devian/80-tools/11-builder/53-data-ndjson/SKILL.md` |
| PB64, pb64, .asset 바이너리 | `skills/devian/80-tools/11-builder/54-data-pb64/SKILL.md` |
| TableGen, 테이블 생성 | `skills/devian/80-tools/11-builder/51-table-codegen/SKILL.md` |
| TableManager, LoadStringsAsync | `skills/devian-unity/20-domain-common-system/31-table-manager/SKILL.md` |
| BundleManager, Addressables | `skills/devian-unity/20-domain-common-system/19-bundle-manager/SKILL.md` |
| UIManager, UICanvas, UIFrame | `skills/devian-unity/30-ui-system/10-ui-manager/skill.md` |
| UI Canvas Frames, UICanvasFrames | `skills/devian-unity/30-ui-system/20-ui-canvas-frames/skill.md` |
| PurchaseManager, purchase, IAP, in-app purchase, 결제, 인앱 | `skills/devian-unity/50-mobile-system/30-purchase-system/00-overview/SKILL.md` |
| purchase audit, refund audit, Google Sheets, spreadsheet log, audit sheet, 결제 로그, 환불 로그, 감사 로그 | `skills/devian-unity/50-mobile-system/30-purchase-system/48-purchase-audit-google-sheets/SKILL.md` |
| purchase audit setup, Google Sheets audit setup, audit sheet setup, spreadsheet permission, spreadsheet share, PURCHASE_AUDIT_SHEET_ID, GOOGLE_DRIVE_AUDIT_CREDENTIALS_JSON | `skills/devian-unity/50-mobile-system/30-purchase-system/13-purchase-audit-google-sheets-setup/SKILL.md` |
| AdManager, advertise, ads, AdMob, GoogleMobileAds, rewarded ad, rewarded, interstitial, banner, app open, 광고, 리워드 광고, 전면 광고, 배너 광고 | `skills/devian-unity/50-mobile-system/47-advertise-system/00-overview/SKILL.md` |

---

## Navigation

| 찾고 싶은 것 | 문서 |
|-------------|------|
| 용어 정의 | [04-glossary](../04-glossary/SKILL.md) |
| Workspace 구조 | [05-workspace](../05-workspace/SKILL.md) |
| 공통 정책 | [01-policy](../01-policy/SKILL.md) |
| 빌드 실행 | [devian/80-tools/11-builder/20-build-pipeline](../80-tools/11-builder/20-build-pipeline/SKILL.md) |
| 테이블 작성 규칙 | [devian/80-tools/11-builder/32-table-authoring](../80-tools/11-builder/32-table-authoring/SKILL.md) |
| 프로토콜 코드젠 | [devian/80-tools/11-builder/33-protocol-spec](../80-tools/11-builder/33-protocol-spec/SKILL.md) |
| Unity 정책 | [devian-unity/01-policy](../../devian-unity/01-policy/SKILL.md) |

---

## Reference

- Index: [skills/devian/SKILL.md](./SKILL.md)
- SSOT: [skills/devian/10-module/03-ssot/SKILL.md](../10-module/03-ssot/SKILL.md)
