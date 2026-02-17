# 10-foundation

Status: ACTIVE  
AppliesTo: v11  
Type: Index / Directory

## Purpose

`com.devian.foundation` 패키지의 `Runtime/Unity/`에 포함된 Unity 컴포넌트들의 인덱스 문서이다.

---

## Components

| ID | 컴포넌트 | 설명 | 스킬 |
|----|----------|------|------|
| 00 | Overview | 진입점/범위 | `00-overview/SKILL.md` |
| 09 | UnityMainThread | Unity 메인스레드 강제 유틸 (UnityMainThread, UnityMainThreadDispatcher) | `22-unity-main-thread/SKILL.md` |
| 31 | Singleton v3 | 2종 싱글톤 (AutoSingleton 기본, CompoSingleton 선택) + Registry SSOT | `14-singleton/SKILL.md` |
| 02 | PoolManager | Type당 1풀 + prefab name 기반 Spawn + Factory 추상화 | `02-pool-manager/SKILL.md` |
| 03 | MessageSystem | ownerKey + enum msgKey 기반 메시지/트리거 시스템 (timer 제외) | `21-message-system/SKILL.md` |
| 04 | PoolFactories | InspectorPoolFactory, BundlePoolFactory | `20-pool-factories/SKILL.md` |
| 10 | AssetManager | AssetBundle 기반 로딩/캐시/언로드 | `17-asset-manager/SKILL.md` |
| 12 | DownloadManager | Addressables Label 기반 Patch/Download (CompoSingleton) | `18-download-manager/SKILL.md` |
| 13 | Pb64Storage | pb64를 Unity TextAsset .asset로 저장하는 규약 | `skills/devian-data/35-pb64-storage/SKILL.md` |
| 14 | TableManager | TB_/ST_ 테이블 로딩/캐시/언로드 (ndjson/pb64) | `10-table-manager/SKILL.md` |
| 15 | SceneTransManager | Scene 전환 직렬화 + 페이드 + BaseScene Enter/Exit | `16-scene-trans-manager/SKILL.md` |
| 16 | SoundTables | TB_SOUND/TB_VOICE 테이블 규약 (컬럼/책임 분리) | `../22-sound-system/16-sound-tables/SKILL.md` |
| 17 | SoundManager | 테이블 기반 사운드 재생/풀/채널/쿨타임 관리 | `../22-sound-system/17-sound-manager/SKILL.md` |
| 18 | VoiceTableResolve | Voice 로딩 시 언어별 Resolve 캐시 + 재생 시 캐시 조회 | `../22-sound-system/18-voice-table-resolve/SKILL.md` |
| 21 | AssetId | 폴더 스캔 기반 Asset ID 선택 UI (Select + 검색) 공통 패턴 | `11-asset-id/SKILL.md` |
| 22 | EffectManager | BundlePool 기반 이펙트 스폰/디스폰 + Runner 확장 | `28-common-effect-manager/SKILL.md` |
| 23 | DevianSettings | config.json → Assets/Settings JSON → Settings.asset 일관 파이프라인 | `13-devian-settings/SKILL.md` |
| 24 | PlayerPrefs Wrapper | Primitive/Enum/Json 기반 PlayerPrefs 래퍼 | `19-player-prefs/SKILL.md` |
| 25 | AnimSequencePlayer | Playables 기반 애니메이션 시퀀스 재생 컴포넌트 | `25-anim-sequence-player/SKILL.md` |
| 26 | FsmController | FIFO 큐 기반 FSM 컨트롤러 (미등록 throw, self-transition 분리) | `23-fsm-controller/SKILL.md` |
| 27 | Bootstrap | Resources 기반 Bootstrap Root + BaseBootstrap 부팅 파이프라인 | `15-bootstrap/SKILL.md` |
| 32 | InputManager | InputActionAsset 기반 입력 수집/정규화/발행 (InputFrame, InputBus, ButtonMap) | `32-input-manager/SKILL.md` |
| 33 | InputController | 오브젝트 부착형 입력 소비 (BaseInputController, IInputSpace) | `33-input-controller/SKILL.md` |
| 34 | VirtualGamepad | InputSystem 커스텀 가상 디바이스 + CompoSingleton Driver | `../50-mobile-system/51-virtual-gamepad/SKILL.md` |

---

## Reference

- Parent: `skills/devian-core/03-ssot/SKILL.md` (Foundation Package SSOT)
- UI 관련 컴포넌트는 `skills/devian-unity/40-ui-system/SKILL.md` 참고
- Object Destruction 규약은 `skills/devian-unity/05-unity-object-destruction/SKILL.md`로 이동됨
