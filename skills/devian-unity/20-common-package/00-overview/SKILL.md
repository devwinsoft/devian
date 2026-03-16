# devian-unity/20-common-package — Overview

Devian Unity 공용 런타임 컴포넌트를 담당한다.

- **Shared Runtime**: Actor/Input, CommonEffect, MaterialEffect, Pool, Singleton, FSM 등 공용 Unity 런타임 컴포넌트
- **Foundation Unity Runtime**: BundleSettings, AssetManager, TableManager, SceneTransManager, BaseApplication 등
- **Domain Common Policy**: Common Domain C#/TS 공통 정책은 `skills/devian/20-domain-common`으로 분리

---

## Start Here

| Document | Description |
|----------|-------------|
| [10-actor-system](../10-actor-system/SKILL.md) | Actor-Controller 패턴 공통 베이스 |
| [11-anim-sequence-player](../11-anim-sequence-player/SKILL.md) | Playables 기반 애니메이션 시퀀스 |
| [12-asset-id](../12-asset-id/SKILL.md) | AssetId selector base 및 선택 규약 |
| [13-asset-manager](../13-asset-manager/SKILL.md) | AssetBundle 기반 로딩/캐시/언로드 |
| [14-base-application](../14-base-application/SKILL.md) | Bootstrap Root + BaseApplication 부팅 |
| [15-common-effect-manager](../15-common-effect-manager/SKILL.md) | BundlePool 기반 공용 이펙트 시스템 |
| [18-bundle-settings](../18-bundle-settings/SKILL.md) | BundleSettings ScriptableObject + Editor 메뉴 |
| [19-bundle-manager](../19-bundle-manager/SKILL.md) | Addressables Label 기반 Patch/Download |
| [20-fsm-controller](../20-fsm-controller/SKILL.md) | FIFO 큐 기반 FSM 컨트롤러 |
| [21-input-controller](../21-input-controller/SKILL.md) | Actor 기반 입력 소비 + InputSpace 전략 |
| [22-input-manager](../22-input-manager/SKILL.md) | InputActionAsset 기반 입력 수집/정규화/발행 |
| [23-material-effect-controller](../23-material-effect-controller/SKILL.md) | Material[] 기반 공용 머티리얼 효과 |
| [24-material-effect-id](../24-material-effect-id/SKILL.md) | MATERIAL_EFFECT_ID 규약 |
| [25-trigger](../25-trigger/SKILL.md) | ownerKey + enum msgKey 기반 메시지/트리거 |
| [26-player-prefs](../26-player-prefs/SKILL.md) | Primitive/Enum/Json 기반 PlayerPrefs 래퍼 |
| [27-pool-system](../27-pool-system/SKILL.md) | Type당 1풀 + prefab name 기반 Spawn + Factory |
| [28-scene-trans-manager](../28-scene-trans-manager/SKILL.md) | Scene 전환 직렬화 + 페이드 |
| [29-singleton](../29-singleton/SKILL.md) | 2종 싱글톤 + Registry SSOT |
| [30-string-table](../30-string-table/SKILL.md) | String Table Feature |
| [31-table-manager](../31-table-manager/SKILL.md) | TB_/ST_ 테이블 로딩/캐시/언로드 |
| [32-unity-utils](../32-unity-utils/SKILL.md) | Unity 유틸리티 (MainThread, Dispatcher 등) |

---

## Domain Common Policy (Moved)

| Document | Description |
|----------|-------------|
| [devian/20-domain-common/00-overview](../../../devian/20-domain-common/00-overview/SKILL.md) | Common Domain 정책 그룹 개요 |
| [devian/20-domain-common/01-policy](../../../devian/20-domain-common/01-policy/SKILL.md) | Common Domain 정책 |
| [devian/20-domain-common/02-module-policy](../../../devian/20-domain-common/02-module-policy/SKILL.md) | Common 모듈 정책 (C#/TS) |
| [devian/20-domain-common/16-common-error](../../../devian/20-domain-common/16-common-error/SKILL.md) | CommonError |
| [devian/20-domain-common/17-common-result](../../../devian/20-domain-common/17-common-result/SKILL.md) | CommonResult |

---

## Related

- [SSOT](../../../devian/10-module/03-ssot/SKILL.md)
- [Devian Index](../../../devian/SKILL.md)
