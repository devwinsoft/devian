# 20-operation/19-page-initial-inventory — Initial Inventory 탭


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
│  Add Reward row: [type] [id listbox] [amount] [ + ]│
│  - Interpretation hint (type별 의미)              │
│  - ID/Amount guide (type별 규칙)                  │
├──────────────────────────────────────────────────┤
│                        [Import Reward IDs] [Save] │
├──────────────────────────────────────────────────┤
│  Status / Validation message                      │
└──────────────────────────────────────────────────┘
```

검증 규칙:
- `type`: `REWARD_TYPE` enum 문자열
- `id`: type별로 listbox 또는 수동 입력
- `amount`: 양의 정수
- `CURRENCY`: `id`는 `/config/rewardIdCatalog.currencyIds`에서 채운다. (`ENUM_TYPES.json:CURRENCY_TYPE` import 결과)
- `EQUIP`/`CARD`/`HERO`: `/config/rewardIdCatalog`에서 로드한 id를 listbox에 채운다.
- `RENTAL`/`PASS`: catalog import 없이 수동 id 입력으로 추가 가능하다.
- 잘못된 row는 저장 전에 에러로 표시


## Behavior

1. 탭(form) 생성 완료 직후 Firestore 문서를 자동 로드한다.
2. 현재 리스트는 우측 `-` 버튼으로 row 삭제 가능하다.
3. 탭 로드시 `/config/rewardIdCatalog`를 읽어 `CURRENCY`/`EQUIP`/`CARD`/`HERO` id listbox 소스로 사용한다.
4. 하단 입력 row에서 `type` 선택 시 listbox 옵션과 `RewardData` 해석 가이드를 갱신한다.
5. `RENTAL`/`PASS` 선택 시 manual id input 모드로 전환된다.
6. 하단 입력 row에서 `+` 버튼으로 신규 reward를 append한다.
7. `Import Reward IDs` 버튼은 local dev server API를 통해 importer 스크립트를 실행한다.
8. importer 결과(stdout/stderr)는 화면 status message에 표시된다.
9. 페이지 하단 `Save` 버튼으로 전체 rewards 배열을 저장한다.


## Interpretation Reference

RewardData 해석 규칙 정본:
- [49-reward-system/11-rewarddata-interpretation](../../../../devian-unity/50-mobile-system/49-reward-system/11-rewarddata-interpretation/SKILL.md)

ID catalog import:
- [20-excel-reward-id-export](../20-excel-reward-id-export/SKILL.md)


---


## Related

- [00-overview](../00-overview/SKILL.md) — Operation 개요
- [15-layout](../15-layout/SKILL.md) — 레이아웃, 스타일 가이드
- [03-ssot](../03-ssot/SKILL.md) — Operation 탭 정본
- [20-excel-reward-id-export](../20-excel-reward-id-export/SKILL.md) — id catalog import
