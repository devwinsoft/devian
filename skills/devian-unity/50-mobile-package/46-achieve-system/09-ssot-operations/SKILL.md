# 09-ssot-operations — 46-achieve-system

Status: ACTIVE
AppliesTo: v10

이 문서는 Achieve 시스템 운영/테스트/DoD 정본이다.

---

## 운영 시나리오

### 1) 앱 시작/로그인

- `InitializeAsync(ct)`
- `SyncAsync(ct)`
- 필요 시 `RefreshRuntimes()`로 UI 초기화 이벤트 재발행
- 초기화 시 `achieve_id` group 기준 runtime을 항상 생성한다.
- `ACHIEVE_SOCIAL`는 `req_msg_id/req_value`, `ACHIEVE_PASS`는 `req_pass_id`/`req_season_id` 조건으로 `WAIT/ACTIVE`를 결정한다.

### 2) gameplay stat 입력

- 상위 로직은 `GameMessageManager.Notify(messageType, delta)`를 호출
- `AchieveManager`는 `GameMessageTrigger` 구독으로 runtime 진행도를 동기화

### 3) 업적 보상 수령

- `ClaimAsync(achievementId, ct)` 호출
- reward apply -> level-up 또는 completed -> save -> platform unlock(best-effort)

---

## 테스트 체크리스트

- `ACHIEVE_SOCIAL`/`ACHIEVE_PASS` row 기준 runtime 생성/복구 정상
- `ACHIEVE_SOCIAL(req_msg_id/req_value)` WAIT 조건 정상
- `ACHIEVE_PASS(req_pass_id)` WAIT 조건 + Inventory PASS_OWNERSHIP_CHANGED 재평가 정상
- level-up 시 condition_msg_id 변경 + projection 동기화 정상
- level-up 시에도 req 조건 재평가로 `WAIT/ACTIVE` 시작 상태가 올바르게 적용
- 동일 업적 `Unlock + Sync` 연속 호출 시 이벤트 1회 보장
- claim 후 save 실패 시 오류 반환
- Editor/미지원 플랫폼 안전 실패

---

## DoD

- runtime 중복 생성 0건(group 기준)
- level-up 바인딩 누수 0건
- 저장 포맷에 `achieve` 섹션 반영 완료
- 공개 경계 플랫폼 ID 비노출
