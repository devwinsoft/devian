---
name: 11-purchase-repo-firebase-functions-setup
description: Define the repository layout and deployment setup for Firebase Functions in the purchase backend. Use when adding Functions to a repo, deciding where firebase config and firestore rules live, or documenting the canonical CLI deployment structure.
---

# 11-purchase-repo-firebase-functions-setup — Repo Setup for Firebase Functions (Serverless Backend)

Status: ACTIVE
AppliesTo: v10

## 문서 경계 (Scope)

- 이 문서는 **Firebase Functions 레포 구성/CLI/배포 셋업 정본**이다.
- 포함: 폴더 위치, 설정 파일 위치, rules/indexes 파일 위치, 배포/에뮬레이터 명령
- 비포함: 함수 내부 구현 로직/Firestore 스키마/멱등 구현 상세 (→ `40`)
- 비포함: 운영 체크리스트/장애 대응/테스트 시나리오 (→ `09`)
- 비포함: 고정 결정사항의 단일 합의 문서 역할 (→ `46`)

## 목적

이 레포에 **Firebase Cloud Functions(서버리스 백엔드)** 를 추가하기 위한 "레포 구성 정본"을 정의한다.

- 콘솔(UI)에서 코드를 "등록"하는 방식이 아니라,
  로컬에서 코드를 작성하고 **Firebase CLI로 배포**하는 흐름을 전제로 한다.
- Purchase 시스템에서 필요한 최소 서버 구성(Functions + Firestore rules)을 레포에 "파일로 존재"하게 만든다.


---


## A. 레포 내 Functions 위치 (정본)

- 정본 위치: `{repoRoot}/functions`
- 현재 레포는 Firebase 표준 위치를 사용한다.
- `firebase.json` 의 `functions[].source` 도 `functions` 로 고정한다.


---


## B. Firebase 설정 파일 (정본)

Functions 위치가 확정되면, `{repoRoot}` 기준 아래 파일이 반드시 존재해야 한다.

### B1. `.firebaserc`
- 목적: Firebase 프로젝트 alias/ID 연결
- 위치: `{repoRoot}/.firebaserc`
- 규칙: `projects.default = {PROJECT_ID}` 형식으로 유지한다.
- 현재 레포 예시: `default -> devian-framwork-example`

### B2. `firebase.json`
- 목적: functions 소스 경로, firestore rules(사용 시) 등의 엔트리포인트
- 위치: `{repoRoot}/firebase.json`
- 현재 정본:
  - `functions[].source = "functions"`
  - `firestore.rules = "firestore.rules"`
  - `firestore.indexes = "firestore.indexes.json"`


---


## C. Firestore rules 파일 위치 (정본)

- 정본 위치: `{repoRoot}/firestore.rules`
- 인덱스 정본 위치: `{repoRoot}/firestore.indexes.json`
- 현재 레포는 rules/indexes 를 repo root 에 둔다.

규칙(하드룰):
- Purchase ledger / entitlements 문서에 대해 **클라이언트 write 금지**
- Functions(서버)만 write


---


## D. Functions 소스 구조 (정본)

Functions root(= A 섹션에서 선택한 위치) 아래에 최소 구성:

- `package.json`
- `tsconfig.json`
- `src/index.ts` (엔트리)
- `src/purchase/verifyPurchase.ts`
- `src/purchase/getEntitlements.ts`
- `src/purchase/ackPurchaseClientGrant.ts`
- `src/purchase/ackPurchaseStoreConfirm.ts`
- `src/purchase/ackRefundApplied.ts`
- `src/purchase/handleGooglePlayNotification.ts`
- `src/purchase/storeVerify.ts`
- `src/purchase/purchaseAuditSheet.ts`

언어/런타임 정본:
- TypeScript 사용
- Node.js `20`
- `main = "lib/index.js"`


---


## E. 배포/로컬 실행 명령 (정본)

함수 내부 구현/검증 로직은 `40-purchase-backend-firebase` 문서를 따른다.

- Firebase CLI 설치 필요
- 의존성 설치:
  - `npm --prefix functions install`
- lint:
  - `npm --prefix functions run lint`
- build:
  - `npm --prefix functions run build`
- 로컬 에뮬레이터:
  - `npm --prefix functions run serve`
- Functions 배포:
  - `npm --prefix functions run deploy -- --project {PROJECT_ID}`
- 전체 Firebase 배포:
  - `firebase deploy --project {PROJECT_ID}`

현재 `functions/package.json` 스크립트:
- `lint`
- `build`
- `serve`
- `deploy`


---


## F. PurchaseManager 연동 전제(정본)

이 레포 구성 스킬은 "서버 코드 등록"을 가능하게 만드는 단계다.
PurchaseManager가 완료되려면, 다음 스킬의 미결정 항목이 추가로 확정되어야 한다.

- 40: Backend(Functions+Firestore) 스키마/멱등
- 41: storePurchaseId 규칙(Apple/Google)
- 42: grants type/id 규칙
- 43: 클라 ↔ 서버 호출 방식(Callable vs HTTP RPC)

운영 체크리스트/보안 점검/최소 테스트 시나리오는 `09-ssot-operations` 문서를 따른다.


---


## DoD

Hard (must be 0)
- [ ] Functions 위치가 `{repoRoot}/functions` 로 확정되어 문서에 반영되어 있다.
- [ ] `.firebaserc`, `firebase.json`의 존재가 문서에 고정되어 있다.
- [ ] Firestore rules/indexes 파일 위치가 repo root 로 확정되어 있다.
- [ ] Functions 소스 최소 구조(엔트리 + 2개 purchase 함수 파일 경로)가 문서에 고정되어 있다.
- [ ] 배포/에뮬레이터 명령이 문서에 포함되어 있다.

Soft
- [ ] 레포 루트에 `README`/`docs` 링크("Functions 배포는 CLI로 한다") 추가
