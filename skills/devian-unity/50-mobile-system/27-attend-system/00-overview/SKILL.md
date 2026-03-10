# 27-attend-system — Overview

Status: ACTIVE
AppliesTo: v10

MobileSystem 샘플의 출석 보상 시스템 개요다.
이 시스템은 `ATTEND` 테이블 기반으로 "일자별 출석 보상 claim"을 처리한다.

핵심 책임:
- `ATTEND` row(`attendId`, `isActive`, `day`, `rewardGroupId`)를 런타임 규칙으로 해석한다.
- 현재 cycle day 기준으로 claim 가능 항목을 계산한다.
- claim 성공 시 `RewardManager`로 보상을 적용한다.
- claim 상태를 `AttendStorage`에 저장/복구한다.
- reset 조건을 고정한다: 정보 없음 / 마지막 수령 후 72시간 경과 / 7일차 수령 다음 날.

---

## Scope

- `ATTEND` 테이블 정렬/활성 row 해석
- 출석 claim 가능 여부 판정
- 출석 claim 처리 + 보상 적용 위임
- 저장/복구를 위한 storage 모델 정의

## Non-goals

- 서버 ledger/멱등/재시도 큐
- ATTEND 외 테이블 규칙 재정의
- Inventory 직접 mutation (반드시 RewardManager 경유)

---

## Start Here

| Document | Description |
|----------|-------------|
| [01-policy](../01-policy/SKILL.md) | 모듈 경계/하드룰 |
| [03-ssot](../03-ssot/SKILL.md) | ATTEND 스키마 + 런타임 규약 정본 |
| [09-ssot-operations](../09-ssot-operations/SKILL.md) | 운영 시나리오/테스트/DoD |
| [10-attend-manager](../10-attend-manager/SKILL.md) | AttendManager 설계 |
| [11-attend-storage](../11-attend-storage/SKILL.md) | AttendStorage 저장 규약 |

---

## Related

- [49-reward-system](../../49-reward-system/00-overview/SKILL.md)
- [21-savedata-system](../../21-savedata-system/00-overview/SKILL.md)
- [26-remote-config-system](../../26-remote-config-system/00-overview/SKILL.md)
- [48-mission-system](../../48-mission-system/00-overview/SKILL.md)
