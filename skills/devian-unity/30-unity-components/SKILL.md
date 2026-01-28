# 30-unity-components

Status: ACTIVE  
AppliesTo: v10  
Type: Index / Directory

## Purpose

`com.devian.unity` 패키지에 포함된 Unity 컴포넌트들의 인덱스 문서이다.

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
| 13 | Pb64Storage | pb64를 Unity TextAsset .asset로 저장하는 규약 | `skills/devian/35-pb64-storage/SKILL.md` (DEPRECATED: `13-pb64-storage/SKILL.md`) |
| 14 | TableManager | TB_/ST_ 테이블 로딩/캐시/언로드 (ndjson/pb64) | `14-table-manager/SKILL.md` |
| 15 | SceneTransManager | Scene 전환 직렬화 + 페이드 + BaseScene Enter/Exit | `15-scene-trans-manager/SKILL.md` |

---

## Reference

- Parent: `skills/devian-unity/20-packages/com.devian.unity/SKILL.md`
