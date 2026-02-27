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
| 09 | UnityUtils | Unity 유틸리티 모음 (MainThread, Dispatcher, UnityCoroutineRunner) | `23-unity-utils/SKILL.md` |
| 31 | Singleton v3 | 2종 싱글톤 (AutoSingleton=script-created, CompoSingleton=scene/prefab-attached) + Registry SSOT | `15-singleton/SKILL.md` |
| 02 | PoolSystem | Type당 1풀 + prefab name 기반 Spawn + Factory 추상화 + InspectorPoolFactory/BundlePoolFactory/BundlePool | `10-pool-system/SKILL.md` |
| 03 | MessageSystem | ownerKey + enum msgKey 기반 메시지/트리거 시스템 (timer 제외) | `22-message-system/SKILL.md` |
| 10 | AssetManager | AssetBundle 기반 로딩/캐시/언로드 | `18-asset-manager/SKILL.md` |
| 12 | DownloadManager | Addressables Label 기반 Patch/Download (CompoSingleton) | `19-download-manager/SKILL.md` |
| 13 | Pb64Storage | pb64를 Unity TextAsset .asset로 저장하는 규약 | `skills/devian-tools/11-builder/35-pb64-storage/SKILL.md` |
| 14 | TableManager | TB_/ST_ 테이블 로딩/캐시/언로드 (ndjson/pb64) | `11-table-manager/SKILL.md` |
| 15 | SceneTransManager | Scene 전환 직렬화 + 페이드 + SceneBase/SceneBoot Enter/Exit | `17-scene-trans-manager/SKILL.md` |
| 24 | PlayerPrefs Wrapper | Primitive/Enum/Json 기반 PlayerPrefs 래퍼 | `20-player-prefs/SKILL.md` |
| 25 | AnimSequencePlayer | Playables 기반 애니메이션 시퀀스 재생 컴포넌트 | `26-anim-sequence-player/SKILL.md` |
| 26 | FsmController | FIFO 큐 기반 FSM 컨트롤러 (미등록 throw, self-transition 분리) | `24-fsm-controller/SKILL.md` |
| 27 | Bootstrap | Resources 기반 Bootstrap Root + BaseBootstrap 부팅 파이프라인 | `16-bootstrap/SKILL.md` |

---

## Reference

- Parent: `skills/devian/10-module/03-ssot/SKILL.md` (Foundation Package SSOT)
- UI 관련 컴포넌트는 `skills/devian-unity/30-ui-system/SKILL.md` 참고
