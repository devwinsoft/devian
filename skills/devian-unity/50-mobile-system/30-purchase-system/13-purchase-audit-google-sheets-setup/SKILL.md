---
name: 13-purchase-audit-google-sheets-setup
description: Set up Google Sheets based purchase and refund audit logging for the Firebase Functions purchase backend. Use when enabling the audit spreadsheet for a new Firebase project, wiring service account and Secret Manager access, or validating that server-side purchase/refund events can append to monthly tabs.
---

# 13-purchase-audit-google-sheets-setup — Google Sheets Audit Setup

Status: ACTIVE
AppliesTo: v10

> Repo 구조 정본: `../11-purchase-repo-firebase-functions-setup/SKILL.md`

> 구매 검증 셋업 정본: `../12-purchase-verification-setup/SKILL.md`

> 감사 로그 구조 정본: `../48-purchase-audit-google-sheets/SKILL.md`


## 문서 경계 (Scope)

- 이 문서는 **Google Sheets purchase/refund audit 로그를 새 Firebase 프로젝트에 붙이는 셋업 실행 가이드 정본**이다.
- 포함: Sheets/Drive API, 서비스 계정, Secret Manager, Spreadsheet 공유, `.env`, 배포 후 스모크 체크
- 비포함: 월별 탭 구조/컬럼 명세/이벤트 타입 정의 (→ `48`)
- 비포함: 구매 검증/RTDN 인프라 자체 (→ `12`)


## 목적

`PurchaseManager` 서버 플로우에서 Firestore와 별도로 Google Sheets 감사 로그를 남기기 위한
**프로젝트별 설정 절차**를 고정한다.

이 셋업의 목표는 다음 두 가지다.

- Firebase Functions 서버가 공유된 Spreadsheet에 row append 할 수 있어야 한다.
- Spreadsheet는 서버가 월별 탭(`YYYY-MM`)만 자동 생성하고, Spreadsheet 파일 자체는 운영자가 미리 만든다.


---


## A. 사전 요구사항

- Functions 레포 구조와 배포 경로가 준비돼 있어야 한다. (→ `11`)
- 구매 검증과 RTDN이 이미 동작 중이면, 이 문서는 **감사 로그 설정만 추가**하면 된다. (→ `12`)
- 대상 Spreadsheet는 운영자가 직접 생성한다.
- 대상 Spreadsheet는 Google Workspace/Shared Drive가 없어도 된다.
- 서비스 계정에 대상 Spreadsheet `Editor` 권한을 부여해야 한다.


---


## B. 필요한 리소스 (정본)

| 항목 | 예시 | 설명 |
|------|------|------|
| `{PROJECT_ID}` | `devian-framwork-example` | Firebase/GCP 프로젝트 ID |
| `{PROJECT_NUMBER}` | `119226054667` | Gen2 runtime service account 계산용 |
| `{REGION}` | `asia-northeast3` | Functions 리전 |
| Audit 서비스 계정 | `purchase-audit-writer@{PROJECT_ID}.iam.gserviceaccount.com` | Spreadsheet write 전용 |
| Secret | `GOOGLE_DRIVE_AUDIT_CREDENTIALS_JSON` | Audit 서비스 계정 JSON 키 저장 |
| Spreadsheet ID | `10PtYQ...` | 운영자가 생성한 대상 Spreadsheet |
| `.env` 키 | `PURCHASE_AUDIT_SHEET_ID` | 대상 Spreadsheet ID |
| `.env` 키(선택) | `PURCHASE_AUDIT_REGION` | 기본값 미사용 시 리전 고정 |

하드룰:

- 서버는 Spreadsheet 파일 자체를 자동 생성하지 않는다.
- 서버는 월별 탭(`YYYY-MM`)만 자동 생성한다.
- raw `.csv` 파일 rewrite 방식은 사용하지 않는다.


---


## C. 셋업 절차

### C1. API 활성화

```bash
gcloud services enable sheets.googleapis.com drive.googleapis.com \
  --project={PROJECT_ID}
```

### C2. Audit 서비스 계정 생성

```bash
gcloud iam service-accounts create purchase-audit-writer \
  --project={PROJECT_ID}
```

### C3. 서비스 계정 키 생성 + Secret Manager 등록

```bash
gcloud iam service-accounts keys create purchase-audit-writer.json \
  --iam-account=purchase-audit-writer@{PROJECT_ID}.iam.gserviceaccount.com \
  --project={PROJECT_ID}

gcloud secrets create GOOGLE_DRIVE_AUDIT_CREDENTIALS_JSON \
  --project={PROJECT_ID}

gcloud secrets versions add GOOGLE_DRIVE_AUDIT_CREDENTIALS_JSON \
  --data-file=purchase-audit-writer.json \
  --project={PROJECT_ID}
```

### C4. Functions runtime service account에 시크릿 권한 부여

```bash
PROJECT_NUMBER=$(gcloud projects describe {PROJECT_ID} --format="value(projectNumber)")

gcloud secrets add-iam-policy-binding GOOGLE_DRIVE_AUDIT_CREDENTIALS_JSON \
  --member="serviceAccount:${PROJECT_NUMBER}-compute@developer.gserviceaccount.com" \
  --role="roles/secretmanager.secretAccessor" \
  --project={PROJECT_ID}
```

### C5. Spreadsheet 생성 + 공유

운영자가 직접 Google Spreadsheet를 1개 생성한 뒤,
아래 계정에 `Editor` 권한을 부여한다.

```text
purchase-audit-writer@{PROJECT_ID}.iam.gserviceaccount.com
```

규칙:

- `overview` 시트는 사용하지 않는다.
- 서버는 현재 월 탭이 없으면 `YYYY-MM` 이름으로 자동 생성한다.
- 예: `2026-03`

### C6. `functions/.env.{PROJECT_ID}` 설정

```env
PURCHASE_AUDIT_SHEET_ID={SPREADSHEET_ID}
PURCHASE_AUDIT_REGION={REGION}
```

### C7. 빌드 + 배포

```bash
npm --prefix functions install
npm --prefix functions run build
firebase deploy --only functions --project={PROJECT_ID}
```


---


## D. 스모크 체크

### D1. 설정 검증

- Spreadsheet 공유 직후, 서비스 계정으로 대상 Spreadsheet metadata 읽기/월 탭 생성이 가능해야 한다.
- 함수 배포 후 `PURCHASE_AUDIT_SHEET_ID is not configured` 경고가 더 이상 나오지 않아야 한다.

### D2. 런타임 검증

구매/환불 이벤트를 실제로 한 번 발생시켜 아래를 확인한다.

| 시나리오 | 기대 결과 |
|----------|----------|
| 신규 구매 승인 | 현재 월 탭에 `verifyStatus=GRANTED` row 추가 |
| 환불 반영 | 현재 월 탭에 `verifyStatus=REFUNDED` row 추가 |
| 권한 회수 반영 | 현재 월 탭에 `verifyStatus=REVOKED` row 추가 |

### D3. 실패 로그 확인

```bash
gcloud logging read \
  'resource.type="cloud_run_revision" AND ("Purchase audit append failed" OR "purchaseAuditSheet")' \
  --project={PROJECT_ID} --limit=20 --freshness=30m \
  --format="table(timestamp,severity,textPayload)"
```


---


## E. 관련 파일 (현재 레포)

| 경로 | 역할 |
|------|------|
| `{repoRoot}/functions/src/purchase/purchaseAuditSheet.ts` | Sheets client, 월별 탭 생성, row append |
| `{repoRoot}/functions/src/purchase/verifyPurchase.ts` | `PURCHASE_GRANTED` write |
| `{repoRoot}/functions/src/purchase/handleGooglePlayNotification.ts` | `PURCHASE_REFUNDED` / `PURCHASE_REVOKED` write |
| `{repoRoot}/functions/.env.{PROJECT_ID}` | Spreadsheet ID / 환경 설정 |


---


## DoD

Hard (must be 0)
- [ ] `sheets.googleapis.com`, `drive.googleapis.com` 가 활성화돼 있다.
- [ ] Audit 전용 서비스 계정이 생성돼 있다.
- [ ] `GOOGLE_DRIVE_AUDIT_CREDENTIALS_JSON` 시크릿이 존재한다.
- [ ] Functions runtime 서비스 계정에 `secretAccessor` 가 부여돼 있다.
- [ ] 대상 Spreadsheet가 서비스 계정에 `Editor` 로 공유돼 있다.
- [ ] `functions/.env.{PROJECT_ID}` 에 `PURCHASE_AUDIT_SHEET_ID` 가 설정돼 있다.
- [ ] 함수 배포 후 현재 월 탭에 row append 가 가능하다.

Soft
- [ ] `PURCHASE_AUDIT_REGION` 이 프로젝트 규칙에 맞게 고정돼 있다.
- [ ] 첫 구매/환불 이벤트로 실제 row 추가까지 확인했다.
