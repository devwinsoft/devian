# 25-recovery-system — Policy


Status: ACTIVE
AppliesTo: v10
Type: Policy / Entry Point


## Purpose

Recovery 시스템의 모듈 경계/하드룰/보안 규약을 정의한다.

- Recovery는 save data의 **이메일 기반 수동 복원**만 담당한다.
- 자동 복구, 클라우드 동기화, 서버 통신은 Recovery의 책임이 아니다.


---


## Hard Rules


### 1) RecoveryManager는 SaveDataManager의 public API만 사용한다

- SaveDataManager의 internal/private 멤버 접근 금지.
- SaveDataJsonCodec 직접 호출 금지.
- 평문 JSON 획득: `ToJson()` — 런타임 상태에서 평문 JSON 생성.
- 평문 JSON 복원: `RestoreFromPlainJsonAsync()` — 평문 JSON으로 런타임 상태 복원.


### 2) 인코딩 파이프라인은 ComplexUtil 난독화만 사용한다

Export (Encode):
```
평문 JSON → ComplexUtil.Encrypt_Base64 → version prefix → .dvn 파일
```

Import (Decode):
```
.dvn 파일 → version parse → ComplexUtil.Decrypt_Base64 → 평문 JSON
```

- 기기별 암호화(SaveLocalDeviceKeyStore AES-GCM)는 디스크 I/O 레이어에만 존재하며, DVN 파이프라인에는 포함되지 않는다.
- 파이프라인 순서를 변경하면 기존 .dvn 파일과 호환이 깨진다.
- 순서 변경이 필요하면 DVN 포맷 버전을 올려야 한다.


### 3) 파일 확장자는 `.dvn`만 사용한다

- `.txt`, `.json`, `.dat` 등 범용 확장자 사용 금지 (다른 앱과 충돌).
- MIME type: `application/octet-stream`.


### 4) .dvn 파일은 무결성 검증을 포함한다

- 디코딩 파이프라인에서 무결성 검증 실패 시 명확한 에러를 반환한다.
- 손상/위조된 .dvn 파일은 조용히 무시하지 않는다.


### 5) RecoveryManager는 서버/네트워크에 의존하지 않는다

- Firebase Functions/Firestore 호출 금지.
- 외부 API 호출 금지.
- 모든 처리는 로컬에서 완결된다.


---


## Non-goals

- 자동 복구 (auto-recovery)
- 클라우드 동기화 연동
- 서버 기반 복원
- 멀티 슬롯 복원
- 복원 이력 관리 (ledger)
