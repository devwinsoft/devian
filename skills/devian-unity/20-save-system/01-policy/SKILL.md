# 20-save-system — Policy


Status: ACTIVE
AppliesTo: v10
SSOT: [03-ssot](../03-ssot/SKILL.md)


---


## 1. Design Goals


- 서버 운영비 최소화(직접 서버 운영 회피)
- 플랫폼 네이티브 Cloud Save를 우선 사용
- Cloud Save와 동일 스키마의 Local Save를 함께 제공
- 저장 데이터는 "작고 단순하게"(수 KB~수십 KB 목표)


---


## 2. Non-goals


- 서버/BaaS 없이 안드↔iOS↔Steam 간 계정/세이브 완전 공유
- 강력한 치팅 방지(최소 무결성만 선택적으로 허용)


---


## 3. Scope


- 지원 플랫폼은 **Unity 한정**이며 다음 3가지 형태만 허용한다:
  - Android (Google Play Games Saved Games)
  - iOS (iCloud)
  - PC (Steam Cloud)


---


## 4. Numbering Rules


- 10대: Cloud/Local 공통 스펙/정책
- 20대: Cloud Save
- 50대: Local Save
- 플랫폼 번호: 1=Android, 2=iOS, 3=PC(Steam)
  - 예: `21-cloudsave-google`, `22-cloudsave-icloud`, `23-cloudsave-steam`
  - 예: `51-localsave-android`, `52-localsave-ios`, `53-localsave-steam`


---


## 5. Data Model Rules (Contract)


- Cloud Save Payload는 버전/타임스탬프를 포함한다.
- `checksum`은 **필수**이며 **SHA-256**을 사용한다.
- 암호화/복호화는 **devian-common Crypto** 기능을 사용한다.
- 암호화 적용 순서: `encrypt(payload) -> checksum(ciphertext)`


### Save Timing — Guidance (Non-normative)

> 아래는 **서비스 레이어 책임**이며 프레임워크가 강제하지 않는다.

- 저장은 이벤트 기반(스테이지 클리어/보상 확정/설정 변경/백그라운드 진입)으로 권장한다.
- 쓰기 디바운스(짧은 시간 다중 변경은 1회 저장)와 "변경 없으면 저장 안 함"을 권장한다.


---


## 6. Local Save Rules


- Local Save는 Cloud Save 스펙과 동일 스키마/슬롯 규칙을 따른다.
- Local Save 경로는 **LocalSaveManager에서 설정** 가능해야 한다.
- 기본 파일명 규칙은 cloudsave 정책과 동일하며, 슬롯→파일명 매핑을 허용한다.


---


## 7. Platform Strategy


- Android: Google Play Games Saved Games
- iOS: iCloud (Key-Value 우선, 필요 시 CloudKit)
- Steam: Steam Cloud (Remote Storage)


플랫폼별 저장소는 서로 다른 세이브로 간주한다(교차 동기화 비목표).


---


## 8. Build / Feature Toggle


Cloud Save는 선택 기능이다. define/asmdef로 플랫폼별 기능을 분리한다.


- `DEV_CLOUDSAVE_GOOGLE`
- `DEV_CLOUDSAVE_ICLOUD`
- `DEV_CLOUDSAVE_STEAM`
