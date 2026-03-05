# Operation Initial Inventory RewardData UI Plan

Status: Done  
Date: 2026-03-06  
Scope: `skills/devian/80-tools/50-operation/19-page-initial-inventory` + `framework-ts/apps/Operation/src/tabs/initial-inventory.ts`

## Goal

`11-rewarddata-interpretation` 규칙을 반영해 Initial Inventory 탭의 RewardData 추가 UI를 고도화한다.

- `type/id/amount`의 의미를 UI에서 명확히 보여준다.
- 타입별 해석 규칙(특히 `RENTAL`, `SEASON_PASS`의 flag 성격)을 입력 단계에서 반영한다.
- 기존 기능(목록 표시, 삭제, 저장, 자동 로드)은 유지한다.

## Tasks

- [x] 현재 19-page-initial-inventory 스킬과 11-rewarddata-interpretation 스킬 간 규칙 매핑 정리
- [x] RewardData 추가 UI 확장 구현
  - [x] 타입별 해석 안내(semantic hint) 표시
  - [x] 타입별 ID 가이드/placeholder 동적 변경
  - [x] `RENTAL`/`SEASON_PASS` 선택 시 amount를 `1`로 고정(의미상 flag)
  - [x] `CURRENCY` ID 형식 검증(ENUM name 스타일)
- [x] 19-page-initial-inventory 스킬 문서 업데이트
  - [x] 11-rewarddata-interpretation 참조 명시
  - [x] 타입별 입력 동작/검증 규칙 반영
- [x] 빌드 검증 (`framework-ts/apps/Operation`)
- [x] 결과 요약 및 남은 리스크 정리

## Notes

- 서버 스키마 강검증은 이미 `getInitialInventory`에 반영됨.
- 본 작업은 Operation UI/스킬 문서 중심이며 서버 로직은 변경하지 않는다.
- 검증: `npm exec vite build` 통과(번들 크기 경고는 기존 수준).
- 잔여 리스크: enum 원본(`CURRENCY_TYPE`, `RENTAL_TYPE`, `SEASON_PASS_TYPE`)을 UI 드롭다운으로 자동 주입하지는 않았음(형식 검증 기반).
