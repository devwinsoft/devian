# 20-save-system — Cloud Save (PC / Steam, Unity)


Status: ACTIVE
AppliesTo: v10


---


## 1. Scope


Unity PC에서 Steam Cloud(Remote Storage) 기반 Cloud Save 구현 가이드.
스펙/정책은 [10-cloudsave-spec](../10-cloudsave-spec/SKILL.md)와 [01-policy](../01-policy/SKILL.md)를 따른다.


---


## 2. Backend


- Steam Cloud (Remote Storage)


---


## 3. Identity


- Steam 계정/클라이언트 로그인에 의존


---


## 4. Storage Model


- Slot 0(`main`)을 기본 파일로 매핑
- 파일명 규칙 예:
  - `cloudsave_main.json`
  - (선택) `cloudsave_backup.json`, `cloudsave_manual.json`
- `checksum`은 SHA-256 필수(암호화 사용 시 ciphertext 기준)


---


## 5. QA Scenarios


- Steam Cloud ON: 저장/로드
- Steam Cloud OFF: 로컬 세이브만
- 오프라인/재접속: 업로드 재시도
- 2PC 충돌: 최신 승리 규칙 확인
