# devian-save — Cloud Save (iOS / iCloud, Unity)


Status: ACTIVE
AppliesTo: v10


---


## 1. Scope


Unity iOS에서 iCloud 기반 Cloud Save 구현 가이드.
스펙/정책은 [10-cloudsave-spec](../10-cloudsave-spec/SKILL.md)와 [01-policy](../01-policy/SKILL.md)를 따른다.


---


## 2. Backend


- 기본: iCloud Key-Value Store
- 필요 시: CloudKit으로 확장(기본 스펙은 Key-Value 우선)


---


## 3. Identity


- Apple ID / iCloud 설정에 의존
- iCloud 비활성/권한 거부 시: Cloud Save 비활성화(로컬 세이브로 동작)


---


## 4. Storage Model


- Slot 0(`main`) 기본
- SavePayload의 `payload` 문자열 저장
- `checksum`은 SHA-256 필수(암호화 사용 시 ciphertext 기준)


---


## 5. QA Scenarios


- iCloud 활성: 저장/로드
- iCloud 비활성: 로컬 세이브만
- 오프라인: 로컬 → 온라인 복귀 후 업로드 재시도
- 2기기 충돌: 최신 승리 규칙 확인
