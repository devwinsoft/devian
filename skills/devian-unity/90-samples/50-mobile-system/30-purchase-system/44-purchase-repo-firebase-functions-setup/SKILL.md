# 44-purchase-repo-firebase-functions-setup — Repo Setup for Firebase Functions (Serverless Backend)

Status: ACTIVE
AppliesTo: v10

## 목적

이 레포에 **Firebase Cloud Functions(서버리스 백엔드)** 를 추가하기 위한 "레포 구성 정본"을 정의한다.

- 콘솔(UI)에서 코드를 "등록"하는 방식이 아니라,
  로컬에서 코드를 작성하고 **Firebase CLI로 배포**하는 흐름을 전제로 한다.
- Purchase 시스템에서 필요한 최소 서버 구성(Functions + Firestore rules)을 레포에 "파일로 존재"하게 만든다.


---


## A. 레포 내 Functions 위치 (정본)

NEEDS CHECK (단일 선택 필요):
- Option A: `{repoRoot}/functions`
- Option B: `{repoRoot}/server/firebase/functions`

선택 기준(권장):
- 서버 관련 코드가 이미 `server/` 트리로 모여있으면 B
- 아니면 Firebase 표준에 맞춰 A


---


## B. Firebase 설정 파일 (정본)

Functions 위치가 확정되면, `{repoRoot}` 기준 아래 파일이 반드시 존재해야 한다.

### B1. `.firebaserc`
- 목적: Firebase 프로젝트 alias/ID 연결

NEEDS CHECK:
- 실제 Firebase projectId / alias는 레포/팀 정책으로 확정해야 한다.

### B2. `firebase.json`
- 목적: functions 소스 경로, firestore rules(사용 시) 등의 엔트리포인트

NEEDS CHECK:
- Firestore rules 파일 경로를 어디로 둘지 C 섹션에서 확정한다.


---


## C. Firestore rules 파일 위치 (정본)

NEEDS CHECK (단일 선택 필요):
- Option A: `{repoRoot}/firestore.rules`
- Option B: `{repoRoot}/server/firebase/firestore.rules`
- Option C: `{repoRoot}/{functionsRoot}/firestore.rules` (functions와 동거)

선택 기준(권장):
- 서버 설정을 `server/firebase/`로 모으면 B
- 간단히 루트에 두고 관리하면 A

규칙(하드룰):
- Purchase ledger / entitlements 문서에 대해 **클라이언트 write 금지**
- Functions(서버)만 write


---


## D. Functions 소스 구조 (정본)

Functions root(= A 섹션에서 선택한 위치) 아래에 최소 구성:

- `package.json`
- `tsconfig.json` (TS 사용 시)
- `src/index.ts` (엔트리)
- `src/purchase/verifyPurchase.ts`
- `src/purchase/getEntitlements.ts`
- (선택) `src/types/*` (요청/응답 타입)

NEEDS CHECK:
- TS vs JS 선택. 권장: TS(타입/계약 강제)


---


## E. 배포/로컬 실행 명령 (정본)

이 스킬은 "명령의 위치"만 고정한다. 실제 값(프로젝트 ID 등)은 NEEDS CHECK로 남긴다.

- Firebase CLI 설치 필요
- 초기화:
  - `firebase init functions`
- 로컬 에뮬레이터(권장):
  - `firebase emulators:start`
- 배포(최소):
  - `firebase deploy --only functions`
- 배포(개별 함수):
  - `firebase deploy --only functions:verifyPurchase,functions:getEntitlements`

NEEDS CHECK:
- 레포에서 공식적으로 사용할 명령(스크립트)을 `package.json`에 넣을지 여부


---


## F. PurchaseManager 연동 전제(정본)

이 레포 구성 스킬은 "서버 코드 등록"을 가능하게 만드는 단계다.
PurchaseManager가 완료되려면, 다음 스킬의 미결정 항목이 추가로 확정되어야 한다.

- 40: Backend(Functions+Firestore) 스키마/멱등
- 41: storePurchaseId 규칙(Apple/Google)
- 42: grants type/id 규칙
- 43: 클라 ↔ 서버 호출 방식(Callable vs HTTP RPC)


---


## DoD

Hard (must be 0)
- [ ] Functions 위치가 Option A/B 중 하나로 확정되어 문서에 반영되어 있다.
- [ ] `.firebaserc`, `firebase.json`의 존재가 문서에 고정되어 있다.
- [ ] Firestore rules 파일 위치가 단일 옵션으로 확정되어 있다.
- [ ] Functions 소스 최소 구조(엔트리 + 2개 purchase 함수 파일 경로)가 문서에 고정되어 있다.
- [ ] 배포/에뮬레이터 명령이 문서에 포함되어 있다.

Soft
- [ ] 레포 루트에 `README`/`docs` 링크("Functions 배포는 CLI로 한다") 추가
