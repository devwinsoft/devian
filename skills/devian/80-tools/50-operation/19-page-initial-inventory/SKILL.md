# 50-operation/19-page-initial-inventory — Initial Inventory 탭


Status: ACTIVE
AppliesTo: v10


## UI

```
Pipeline: Firestore /config/initialInventory read/write (auto-load on form init)

┌──────────────────────────────────────────────────┐
│  Current Reward List (type / id / amount)        │
│  ┌──────────────────────────────────────────┐    │
│  │ ...                                [-]   │    │
│  │ ...                                [-]   │    │
│  └──────────────────────────────────────────┘    │
├──────────────────────────────────────────────────┤
│  Add Reward row: [type] [id] [amount] [ + ]      │
├──────────────────────────────────────────────────┤
│                                           [Save] │
├──────────────────────────────────────────────────┤
│  Status / Validation message                      │
└──────────────────────────────────────────────────┘
```

검증 규칙:
- `type`: `REWARD_TYPE` enum 문자열
- `id`: 공백 불가
- `amount`: 양의 정수
- 잘못된 row는 저장 전에 에러로 표시


## Behavior

1. 탭(form) 생성 완료 직후 Firestore 문서를 자동 로드한다.
2. 현재 리스트는 우측 `-` 버튼으로 row 삭제 가능하다.
3. 하단 입력 row에서 `+` 버튼으로 신규 reward를 append한다.
4. 페이지 하단 `Save` 버튼으로 전체 rewards 배열을 저장한다.


---


## Related

- [00-overview](../00-overview/SKILL.md) — Operation 개요
- [15-layout](../15-layout/SKILL.md) — 레이아웃, 스타일 가이드
