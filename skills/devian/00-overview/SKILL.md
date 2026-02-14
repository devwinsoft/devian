# Devian v10 — Overview

Status: ACTIVE
AppliesTo: v10
SSOT: skills/devian-core/03-ssot/SKILL.md

## What is Devian?

Devian은 **DATA**와 **PROTOCOL** 두 축으로 구성된 코드 생성 프레임워크다.

| 축 | 역할 | 입력 | 출력 |
|-----|------|------|------|
| **DATA** | 테이블/계약 타입 생성 | XLSX, JSON contracts | C#/TS Entity, Container, NDJSON/pb64 |
| **PROTOCOL** | 네트워크 메시지 생성 | Protocol JSON | C#/TS Message, Codec, Stub |

---

## Policy SSOT

모든 정책/경로/규칙의 정본은 **SSOT 문서**에 있다:

→ `skills/devian-core/03-ssot/SKILL.md`

이 문서를 포함한 모든 스킬은 SSOT를 참조하며, 충돌 시 SSOT가 우선한다.

---

## Devian Skill Groups

| Group | Description | Overview | Policy | SSOT |
|-------|-------------|----------|--------|------|
| **devian-core** | Root SSOT, 스킬 규격, 런타임 | [00-overview](../../devian-core/00-overview/SKILL.md) | [01-policy](../../devian-core/01-policy/SKILL.md) | [03-ssot](../../devian-core/03-ssot/SKILL.md) |
| **devian-tools** | Builder, CLI, 아카이브 | [00-overview](../../devian-tools/00-overview/SKILL.md) | [01-policy](../../devian-tools/01-policy/SKILL.md) | [03-ssot](../../devian-tools/03-ssot/SKILL.md) |
| **devian-data** | Table, Contract, NDJSON, PB64 | [00-overview](../../devian-data/00-overview/SKILL.md) | [01-policy](../../devian-data/01-policy/SKILL.md) | [03-ssot](../../devian-data/03-ssot/SKILL.md) |
| **devian-protocol** | Protocol codegen, Opcode, Registry | [00-overview](../../devian-protocol/00-overview/SKILL.md) | [01-policy](../../devian-protocol/01-policy/SKILL.md) | [03-ssot](../../devian-protocol/03-ssot/SKILL.md) |
| **devian-common** | Common 도메인, Feature 모듈 | [00-overview](../../devian-common/00-overview/SKILL.md) | [01-policy](../../devian-common/01-policy/SKILL.md) | — |
| **devian-unity** | UPM 패키지, Unity 런타임, 컴포넌트 | [00-overview](../../devian-unity/00-overview/SKILL.md) | [01-policy](../../devian-unity/01-policy/SKILL.md) | [03-ssot](../../devian-unity/03-ssot/SKILL.md) |
| **devian-examples** | 예제 도메인, 예제 프로토콜 | [00-overview](../../devian-examples/00-overview/SKILL.md) | [01-policy](../../devian-examples/01-policy/SKILL.md) | — |
| **devian-unity/90-samples** | Unity 샘플 작성 가이드, 네트워크 샘플 | [00-overview](../../devian-unity/90-samples/00-overview/SKILL.md) | [01-policy](../../devian-unity/90-samples/01-policy/SKILL.md) | — |

---

## Routing (Central)

이 문서는 Devian 전체의 **단일 라우팅 정본**이다.
키워드/의도 기반으로 어디 스킬로 가야 하는지 여기서만 결정한다.

### Group Routing

- Unity 일반 컴포넌트(Non-UI) → `skills/devian-unity/10-base-system/00-overview/skill.md`
- Unity UI 컴포넌트(UI/Canvas/Frame/UIManager) → `skills/devian-unity/40-ui-system/00-overview/skill.md`

### Routing Keywords

| keyword | route-to |
|---|---|
| StringTable, string-table, ST_, 다국어, localization, localizing, TEXT table, LocalizedText, 번역 | `skills/devian-common/14-feature-string-table/SKILL.md` |
| NDJSON, ndjson, .json 스토리지 | `skills/devian-data/34-ndjson-storage/SKILL.md` |
| PB64, pb64, .asset 바이너리 | `skills/devian-data/35-pb64-storage/SKILL.md` |
| TableGen, 테이블 생성 | `skills/devian-data/42-tablegen-implementation/SKILL.md` |
| TableManager, LoadStringsAsync | `skills/devian-unity/10-base-system/14-table-manager/SKILL.md` |
| DownloadManager, Addressables | `skills/devian-unity/10-base-system/12-download-manager/SKILL.md` |
| UIManager, UICanvas, UIFrame | `skills/devian-unity/40-ui-system/10-ui-manager/skill.md` |
| UI Canvas Frames, UICanvasFrames | `skills/devian-unity/40-ui-system/20-ui-canvas-frames/skill.md` |
| Cloud Save, CloudSave, cloudsave, Local Save, LocalSave, localsave, save-manager, SaveManager, Save Manager, Cloud Sync, iCloud, Steam Cloud, GPGS | `skills/devian-unity/20-save-system/00-overview/SKILL.md` |

---

## Navigation

| 찾고 싶은 것 | 문서 |
|-------------|------|
| 용어 정의 | [10-glossary](../10-glossary/SKILL.md) |
| Workspace 구조 | [20-workspace](../20-workspace/SKILL.md) |
| 공통 정책 | [01-policy](../01-policy/SKILL.md) |
| 빌드 실행 | [devian-tools/20-build-domain](../../devian-tools/20-build-domain/SKILL.md) |
| 테이블 작성 규칙 | [devian-data/30-table-authoring-rules](../../devian-data/30-table-authoring-rules/SKILL.md) |
| 프로토콜 코드젠 | [devian-protocol/40-codegen-protocol](../../devian-protocol/40-codegen-protocol/SKILL.md) |
| Unity 정책 | [devian-unity/01-policy](../../devian-unity/01-policy/SKILL.md) |
| Cloud/Local Save | [devian-unity/20-save-system/00-overview](../../devian-unity/20-save-system/00-overview/SKILL.md) |

---

## Reference

- Index: [skills/devian/SKILL.md](./SKILL.md)
- SSOT: [skills/devian-core/03-ssot/SKILL.md](../../devian-core/03-ssot/SKILL.md)
