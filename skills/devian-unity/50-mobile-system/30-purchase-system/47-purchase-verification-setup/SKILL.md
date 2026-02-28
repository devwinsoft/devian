# 47-purchase-verification-setup — Purchase Verification Infrastructure Setup

Status: ACTIVE
AppliesTo: v10

> Repo 구조 정본: `../44-purchase-repo-firebase-functions-setup/SKILL.md`

> 결정사항 정본: `../46-purchase-decisions/SKILL.md`

> 환불 처리 정본: `../45-purchase-refund-processing/SKILL.md`


## 문서 경계 (Scope)

- 이 문서는 **구매 검증 인프라를 새 프로젝트에 셋업하는 실행 가이드 정본**이다.
- 포함: Firebase 프로젝트, Secret Manager, IAM, Pub/Sub, Play Console, Firestore, 배포, 검증
- 비포함: 함수 내부 로직(→ `40`), Callable 계약(→ `46`), 환불 핸들러 상세(→ `45`)


---


## A. 사전 요구사항 (Prerequisites)

### A1. CLI 도구

```bash
# Firebase CLI
npm install -g firebase-tools
firebase login

# Google Cloud SDK
# https://cloud.google.com/sdk/docs/install
gcloud auth login
```

### A2. 필요 정보

| 항목 | 예시 | 설명 |
|------|------|------|
| `{PROJECT_ID}` | `devian-framwork-example` | Firebase/GCP 프로젝트 ID |
| `{REGION}` | `asia-northeast3` | Functions 배포 리전 |
| `{PACKAGE_NAME}` | `com.devian.framework` | Android 앱 패키지명 |
| `{RTDN_TOPIC}` | `devian-play-rtdn` | RTDN Pub/Sub 토픽명 |
| Google Play 서비스 계정 JSON | — | Play Console API 접근용 |
| Apple Shared Secret | — | App Store 영수증 검증용 |


---


## B. 자동 셋업 (CLI 명령)

> 아래 명령을 순서대로 실행한다. `{...}` 플레이스홀더를 실제 값으로 치환한다.

### B1. Firebase 프로젝트 연결

```bash
# 레포 루트에서
firebase use {PROJECT_ID}
```

### B2. Secret Manager 등록

```bash
# Google Play 서비스 계정 JSON (파일 내용을 시크릿으로)
gcloud secrets create GOOGLE_APPLICATION_CREDENTIALS_JSON \
  --project={PROJECT_ID}

gcloud secrets versions add GOOGLE_APPLICATION_CREDENTIALS_JSON \
  --data-file={서비스계정JSON파일경로} \
  --project={PROJECT_ID}

# Apple Shared Secret
gcloud secrets create APPLE_SHARED_SECRET \
  --project={PROJECT_ID}

echo -n "{APPLE_SHARED_SECRET_VALUE}" | \
  gcloud secrets versions add APPLE_SHARED_SECRET \
  --data-file=- \
  --project={PROJECT_ID}
```

### B3. Secret Manager 접근 권한

```bash
# Functions 런타임 서비스 계정에 시크릿 접근 허용
gcloud secrets add-iam-policy-binding GOOGLE_APPLICATION_CREDENTIALS_JSON \
  --member="serviceAccount:{PROJECT_ID}@appspot.gserviceaccount.com" \
  --role="roles/secretmanager.secretAccessor" \
  --project={PROJECT_ID}

gcloud secrets add-iam-policy-binding APPLE_SHARED_SECRET \
  --member="serviceAccount:{PROJECT_ID}@appspot.gserviceaccount.com" \
  --role="roles/secretmanager.secretAccessor" \
  --project={PROJECT_ID}
```

### B4. Pub/Sub 토픽 생성 (RTDN용)

```bash
gcloud pubsub topics create {RTDN_TOPIC} --project={PROJECT_ID}
```

### B5. IAM — Google Play → Pub/Sub (Publisher)

```bash
gcloud pubsub topics add-iam-policy-binding {RTDN_TOPIC} \
  --member="serviceAccount:google-play-developer-notifications@system.gserviceaccount.com" \
  --role="roles/pubsub.publisher" \
  --project={PROJECT_ID}
```

> `google-play-developer-notifications@system.gserviceaccount.com` 은 Google 관리 시스템 계정. 프로젝트에 생성 불필요.

### B6. 환경 변수 파일 생성

```bash
# functions/.env.{PROJECT_ID}
cat > functions/.env.{PROJECT_ID} << 'EOF'
APPLE_SHARED_SECRET=
RTDN_TOPIC={RTDN_TOPIC}
EOF
```

> `APPLE_SHARED_SECRET` env 값은 비워도 됨 (Secret Manager에서 읽음). 레거시 호환용.

### B7. 빌드 + 배포

```bash
# Functions 빌드
cd functions && npm install && npm run build && cd ..

# 전체 배포 (Functions + Firestore indexes + rules)
firebase deploy --project={PROJECT_ID}
```

### B8. IAM — Pub/Sub → Cloud Run (Invoker)

> Functions 배포 후 Cloud Run 서비스가 생성된 다음 실행해야 한다.

```bash
# 프로젝트 번호 확인
PROJECT_NUMBER=$(gcloud projects describe {PROJECT_ID} --format="value(projectNumber)")

# Cloud Run invoker 역할 부여
gcloud run services add-iam-policy-binding handlegoogleplaynotification \
  --region={REGION} \
  --member="serviceAccount:${PROJECT_NUMBER}-compute@developer.gserviceaccount.com" \
  --role="roles/run.invoker" \
  --project={PROJECT_ID}
```


---


## C. 수동 설정 (Console UI 필수)

### C1. Google Play Console — 서비스 계정

1. [Google Cloud Console](https://console.cloud.google.com/iam-admin/serviceaccounts) 에서 서비스 계정 생성
2. JSON 키 다운로드
3. [Google Play Console](https://play.google.com/console) → 설정 → API 액세스
4. 서비스 계정 연결 + **재무 데이터** 권한 부여
5. B2에서 JSON 키를 Secret Manager에 등록

### C2. Google Play Console — RTDN

1. Google Play Console → 설정 → 수익 창출 설정 (Monetization setup)
2. **Real-time developer notifications** 섹션
3. 토픽 입력: `projects/{PROJECT_ID}/topics/{RTDN_TOPIC}`
4. **저장** 클릭
5. **Send test notification** → Cloud Run 로그에서 `Test notification received` 확인

### C3. Apple App Store (해당 시)

1. [App Store Connect](https://appstoreconnect.apple.com) → 앱 → 앱 내 구입 → 공유 비밀
2. 공유 비밀(Shared Secret) 복사
3. B2에서 Secret Manager에 등록


---


## D. 검증 체크리스트

### D1. 자동 검증 (CLI)

```bash
# 1) Functions 상태 확인
gcloud functions list --project={PROJECT_ID} --format="table(name,state,updateTime)"

# 2) Pub/Sub 토픽 + 구독 확인
gcloud pubsub topics describe projects/{PROJECT_ID}/topics/{RTDN_TOPIC}
gcloud pubsub topics list-subscriptions projects/{PROJECT_ID}/topics/{RTDN_TOPIC}

# 3) Cloud Run IAM 확인
gcloud run services get-iam-policy handlegoogleplaynotification \
  --region={REGION} --project={PROJECT_ID}

# 4) Firestore 인덱스 상태 확인
gcloud firestore indexes composite list \
  --project={PROJECT_ID} --database="(default)" \
  --format="table(name.basename(),state,queryScope,fields)"

# 5) RTDN 파이프라인 테스트 (수동 메시지 발행)
gcloud pubsub topics publish projects/{PROJECT_ID}/topics/{RTDN_TOPIC} \
  --message='{"version":"1.0","packageName":"com.test","eventTimeMillis":"0","testNotification":{"version":"1.0"}}'

# 6) Cloud Run 로그 확인
gcloud logging read \
  'resource.type="cloud_run_revision" AND resource.labels.service_name="handlegoogleplaynotification"' \
  --project={PROJECT_ID} --limit=10 \
  --format="table(timestamp,severity,textPayload)" --freshness=5m
```

### D2. 검증 매트릭스

| # | 항목 | 명령/방법 | 기대 결과 |
|---|------|----------|----------|
| 1 | Functions 배포 | D1-1 | 모든 함수 ACTIVE |
| 2 | Pub/Sub 토픽 | D1-2 | 토픽 존재 + eventarc 구독 1개 |
| 3 | Cloud Run IAM | D1-3 | `roles/run.invoker` 바인딩 |
| 4 | Firestore 인덱스 | D1-4 | 모든 인덱스 READY |
| 5 | RTDN 연결 | D1-5 + D1-6 | `Test notification received` 로그 |
| 6 | Play Console 연결 | C2-5 | `Test notification received. pkg={PACKAGE_NAME}` |
| 7 | 구매 검증 | 앱에서 테스트 구매 | Firestore에 purchase 문서 생성, `verifyStatus=GRANTED` |
| 8 | 환불 처리 | 시뮬레이션 RTDN | Firestore `verifyStatus=REFUNDED` |


---


## E. 프로젝트 파일 구조 (현재 정본)

```
{repoRoot}/
├── .firebaserc                          # Firebase 프로젝트 연결
├── firebase.json                        # Functions/Firestore 설정
├── firestore.rules                      # Firestore 보안 규칙
├── firestore.indexes.json               # Firestore 인덱스 정의
└── functions/
    ├── package.json                     # Node 20, firebase-functions v6
    ├── tsconfig.json
    ├── .env.{PROJECT_ID}                # 프로젝트별 환경 변수
    └── src/
        ├── index.ts                     # 엔트리 (setGlobalOptions + exports)
        ├── purchase/
        │   ├── verifyPurchase.ts        # 구매 검증 Callable
        │   ├── ackPurchaseClientGrant.ts
        │   ├── ackPurchaseStoreConfirm.ts
        │   ├── getEntitlements.ts
        │   ├── getRecentPurchases30d.ts
        │   ├── getPurchaseAdjustments.ts
        │   ├── ackRefundApplied.ts
        │   ├── handleGooglePlayNotification.ts  # RTDN Pub/Sub 핸들러
        │   └── storeVerify.ts           # Google/Apple 스토어 검증
        └── ads/
            └── verifyAdReward.ts        # AdMob SSV
```


---


## F. 환경 변수 / 시크릿 총정리

### F1. 환경 변수 (`.env.{PROJECT_ID}`)

| 키 | 예시 | 용도 |
|----|------|------|
| `RTDN_TOPIC` | `devian-play-rtdn` | RTDN Pub/Sub 토픽명 |
| `APPLE_SHARED_SECRET` | (비어있음) | 레거시, Secret Manager 사용 |

### F2. Secret Manager

| 시크릿 이름 | 용도 | 접근 주체 |
|------------|------|----------|
| `GOOGLE_APPLICATION_CREDENTIALS_JSON` | Google Play Developer API 인증 | `verifyPurchase`, `handleGooglePlayNotification` |
| `APPLE_SHARED_SECRET` | Apple 영수증 검증 | `verifyPurchase` |

### F3. 전역 설정 (`index.ts`)

| 설정 | 값 | 방법 |
|------|-----|------|
| `region` | `asia-northeast3` | `setGlobalOptions({region: "asia-northeast3"})` |

### F4. IAM 바인딩 총정리

| 대상 리소스 | 서비스 계정 | 역할 | 목적 |
|------------|-----------|------|------|
| Pub/Sub 토픽 | `google-play-developer-notifications@system.gserviceaccount.com` | `roles/pubsub.publisher` | Google Play → Pub/Sub |
| Cloud Run 서비스 | `{PROJECT_NUMBER}-compute@developer.gserviceaccount.com` | `roles/run.invoker` | Pub/Sub → Cloud Run |
| Secret Manager | `{PROJECT_ID}@appspot.gserviceaccount.com` | `roles/secretmanager.secretAccessor` | Functions → 시크릿 |


---


## G. 트러블슈팅

### G1. Cloud Run 인증 오류

```
WARNING: The request was not authenticated.
```

원인: Pub/Sub 구독 서비스 계정에 `roles/run.invoker` 없음.
해결: B8 실행.

### G2. Firestore 인덱스 오류

```
Error: 9 FAILED_PRECONDITION: The query requires an index.
```

원인: 복합 인덱스 미배포 또는 빌드 중.
해결:
```bash
firebase deploy --only firestore:indexes --project={PROJECT_ID}
# 인덱스 빌드 상태 확인 (READY될 때까지 대기)
gcloud firestore indexes composite list --project={PROJECT_ID} --database="(default)"
```

### G3. 시크릿 접근 오류

```
Error: 7 PERMISSION_DENIED: Permission denied on secret
```

원인: Functions 서비스 계정에 `secretAccessor` 없음.
해결: B3 실행.

### G4. RTDN 미수신 (라이선스 테스터)

Google Play는 테스트 구매에 `voidedPurchaseNotification` RTDN을 보내지 않는다.
해결: D1-5의 수동 메시지 발행으로 핸들러 검증.

### G5. storeProductId 재검증 실패

```
The purchase token does not match the product ID.
```

원인: purchase 문서에 `storeProductId` 미저장 (구버전 구매).
동작: 재검증 건너뜀 (RTDN 자체 신뢰). 향후 구매는 `storeProductId` 자동 저장.


---


## H. 관련 정본 링크

- Repo 구조(44): `../44-purchase-repo-firebase-functions-setup/SKILL.md`
- Refund(45): `../45-purchase-refund-processing/SKILL.md`
- Decisions(46): `../46-purchase-decisions/SKILL.md`
- Backend(40): `../40-purchase-backend-firebase/SKILL.md`
- Operations(09): `../09-ssot-operations/SKILL.md`


---


## DoD

Hard (must be 0)
- [x] Secret Manager 시크릿 2개 등록 + IAM 접근 설정
- [x] Pub/Sub 토픽 생성 + Google Play Publisher IAM
- [x] Cloud Run Invoker IAM (Functions 배포 후)
- [x] Firestore 인덱스 배포 + READY 확인
- [x] Play Console RTDN 토픽 설정 + 테스트 알림 수신
- [x] 앱 테스트 구매 → Firestore `verifyStatus=GRANTED` 확인

Soft
- [ ] Apple App Store Server Notifications v2 셋업
- [ ] CI/CD 파이프라인 (자동 배포)
