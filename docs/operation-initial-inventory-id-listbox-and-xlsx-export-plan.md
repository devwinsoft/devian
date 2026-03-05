# Operation Initial Inventory ID Listbox + XLSX Import Plan

Status: Done  
Date: 2026-03-06  
Scope: Initial Inventory UI id listbox 전환 + xlsx id import skill/script

## Goal

- Initial Inventory의 `RewardData.id`를 text input이 아닌 drop listbox로 전환한다.
- `type` 변경 시 `id` listbox 옵션이 동적으로 바뀌게 한다.
- 정책:
  - `CURRENCY`: 코드 enum(`CURRENCY_TYPE`) 기반 옵션
  - `RENTAL`, `SEASON_PASS`: 선택 불가
  - `EQUIP`, `CARD`, `HERO`: xlsx에서 추출해 서버(Firestore)로 import된 catalog를 listbox에 사용
- 위 import 작업을 반복 가능하게 만드는 신규 스킬을 추가한다.

## Tasks

- [x] enum/table 원천 경로 확인 (`CURRENCY_TYPE`, ItemTable.xlsx, UnitTable.xlsx)
- [x] Firestore config 경로 추가
  - [x] `/config/rewardIdCatalog` read/write rules 반영
- [x] Operation UI 구현
  - [x] id input -> select listbox 전환
  - [x] type별 옵션 소스 분기
  - [x] RENTAL/SEASON_PASS 비선택 처리
  - [x] rewardIdCatalog 로드 로직 추가
- [x] xlsx -> Firestore import 스크립트 추가
  - [x] EQUIP/CARD/HERO id 추출
  - [x] `/config/rewardIdCatalog` 저장
  - [x] dry-run 지원
- [x] 신규 스킬 추가 (`50-operation` 하위)
  - [x] import 스크립트 사용법
  - [x] 입력 파일/시트/컬럼 규약
  - [x] Firestore 문서 스키마 명시
- [x] 기존 Operation 스킬 문서 동기화
- [x] build + script 검증
- [x] 계획서 완료 처리

## Notes

- `11-rewarddata-interpretation`을 Initial Inventory 입력 규칙의 참조로 유지한다.
- 검증:
  - `npm exec vite build` 통과
  - `npm run import:reward-id-catalog -- --dry-run` 통과
  - `npm run import:reward-id-catalog` 통과
