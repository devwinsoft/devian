# 30-unity-components

Status: ACTIVE  
AppliesTo: v10  
Type: Index / Directory

## Purpose

`com.devian.foundation` 패키지의 `Runtime/Unity/`에 포함된 Unity 컴포넌트들의 인덱스 문서이다.

---

## Components

| ID | 컴포넌트 | 설명 | 스킬 |
|----|----------|------|------|
| 00 | UnityObjectDestruction | UnityEngine.Object.Destroy / DestroyImmediate 규약 | `00-unity-object-destruction/SKILL.md` |
| 01 | Singleton | Persistent MonoBehaviour Singleton (3종: MonoSingleton, AutoSingleton, ResSingleton) | `01-singleton/SKILL.md` |
| 02 | PoolManager | Type당 1풀 + prefab name 기반 Spawn + Factory 추상화 | `02-pool-manager/SKILL.md` |
| 03 | MessageSystem | instanceId + key 기반 메시지/트리거 시스템 (timer 제외) | `03-message-system/SKILL.md` |
| 04 | PoolFactories | InspectorPoolFactory, BundlePoolFactory | `04-pool-factories/SKILL.md` |
| 10 | AssetManager | AssetBundle 기반 로딩/캐시/언로드 | `10-asset-manager/SKILL.md` |
| 11 | NetWsClientBehaviourBase | WebSocket 네트워크 클라이언트 베이스 | `11-network-client-behaviour/SKILL.md` |
| 12 | DownloadManager | Addressables Label 기반 Patch/Download (ResSingleton, inspector label list) | `12-download-manager/SKILL.md` |
| 13 | Pb64Storage | pb64를 Unity TextAsset .asset로 저장하는 규약 | `skills/devian/35-pb64-storage/SKILL.md` |
| 14 | TableManager | TB_/ST_ 테이블 로딩/캐시/언로드 (ndjson/pb64) | `14-table-manager/SKILL.md` |
| 15 | SceneTransManager | Scene 전환 직렬화 + 페이드 + BaseScene Enter/Exit | `15-scene-trans-manager/SKILL.md` |
| 16 | SoundTables | TB_SOUND/TB_VOICE 테이블 규약 (컬럼/책임 분리) | `16-sound-tables/SKILL.md` |
| 17 | SoundManager | 테이블 기반 사운드 재생/풀/채널/쿨타임 관리 | `17-sound-manager/SKILL.md` |
| 18 | VoiceTableResolve | Voice 로딩 시 언어별 Resolve 캐시 + 재생 시 캐시 조회 | `18-voice-table-resolve/SKILL.md` |
| 21 | AssetId | 폴더 스캔 기반 Asset ID 선택 UI (Select + 검색) 공통 패턴 | `21-asset-id/SKILL.md` |
| 22 | EffectManager | BundlePool 기반 이펙트 스폰/디스폰 + Runner 확장 | `22-effect-manager/SKILL.md` |
| 23 | DevianSettings | config.json → Assets/Settings JSON → Settings.asset 일관 파이프라인 | `23-devian-settings/SKILL.md` |
| 24 | PlayerPrefs Wrapper | Primitive/Enum/Json 기반 PlayerPrefs 래퍼 | `24-player-prefs/SKILL.md` |
| 25 | AnimSequencePlayer | Playables 기반 애니메이션 시퀀스 재생 컴포넌트 | `25-anim-sequence-player/SKILL.md` |
| 26 | FsmController | FIFO 큐 기반 FSM 컨트롤러 (미등록 throw, self-transition 분리) | `26-fsm-controller/SKILL.md` |
| 27 | BootstrapResourceObject | Resources 기반 Bootstrap Root + IDevianBootStep 부팅 파이프라인 | `27-bootstrap-resource-object/SKILL.md` |

---

## Reference

- Parent: `skills/devian/03-ssot/SKILL.md` (Foundation Package SSOT)
