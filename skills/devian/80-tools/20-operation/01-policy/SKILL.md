# 20-operation — Policy


Status: ACTIVE
AppliesTo: v10


## 1) localhost 전용 (Hard Rule)

- Operation 웹앱은 **localhost에서만 실행**한다. 외부 배포 금지.
- AES 공유키가 클라이언트 JS에 포함되므로, 네트워크 노출 시 키 유출 위험이 있다.
- 외부 접근 가능한 호스트(0.0.0.0 등)에 바인딩하지 않는다.


## 2) 바닐라 TS (Hard Rule)

- UI 프레임워크(React, Vue, Svelte 등)를 사용하지 않는다.
- 바닐라 TypeScript + 경량 라이브러리만 허용한다.
- DOM 조작은 직접 수행한다.


## 3) 암호키 보안

- AES 공유키는 Unity 클라이언트와 **동일**해야 한다.
- 공유키 정본: [25-recovery-system/03-ssot](../../../../devian-unity/50-mobile-system/25-recovery-system/03-ssot/SKILL.md) §C
- 소스 코드에 키를 직접 포함한다 (localhost 전용이므로 허용).
- Git에 push하는 경우 `.gitignore` 또는 환경변수 분리를 검토한다.


## 4) 인코딩 파이프라인 동기화

- 각 encode/decode 기능의 파이프라인은 Unity 클라이언트와 **완전히 동일**해야 한다.
- DVN 파이프라인 정본: [25-recovery-system/03-ssot](../../../../devian-unity/50-mobile-system/25-recovery-system/03-ssot/SKILL.md)
- 파이프라인 변경 시 양쪽을 동시에 업데이트한다.


## 5) TypeScript Workspace 정책

- `framework-ts/` 워크스페이스 정책을 따른다.
- 정본: [Tools SSOT](../../03-ssot/SKILL.md) §TypeScript Workspace 정본


## 6) Firestore Config 문서 정책

- Operation에서 직접 수정 가능한 config 문서는 아래 두 경로로 제한한다.
  - `/config/appVersion`
  - `/config/initialInventory`
- Initial Inventory id catalog import 문서는 아래 경로를 사용한다.
  - `/config/rewardIdCatalog`
- `/config/initialInventory` 저장 시 `rewards: RewardData[]` 스키마 검증을 선행한다.
- 서버(`getInitialInventory`)도 동일 스키마를 강검증하며, invalid 데이터가 있으면 `failed-precondition`을 반환하고 marker를 남기지 않는다.
- 초기 지급은 서버 callable(`getInitialInventory`)의 transaction marker로 1회 보장되므로, 클라이언트에서 중복 지급 로직을 추가하지 않는다.


---


## Related

- [00-overview](../00-overview/SKILL.md) — Operation 개요
- [25-recovery-system/03-ssot](../../../../devian-unity/50-mobile-system/25-recovery-system/03-ssot/SKILL.md) — DVN 파이프라인/AES 정본
- [Tools SSOT](../../03-ssot/SKILL.md) — Tools SSOT
