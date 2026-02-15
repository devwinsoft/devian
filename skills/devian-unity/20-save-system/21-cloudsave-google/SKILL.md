# 20-save-system — Cloud Save (Android / Google, Unity)


Status: ACTIVE
AppliesTo: v10


---


## 1. Scope


Unity Android에서 Google Play Games Saved Games 기반 Cloud Save 구현 가이드.
스펙/정책은 [10-cloudsave-spec](../10-cloudsave-spec/SKILL.md)와 [01-policy](../01-policy/SKILL.md)를 따른다.


---


## 2. Backend


- Google Play Games Saved Games 사용


---


## 3. Identity


- Google Play Games 로그인 기반
- 로그인 불가/거부 시: Cloud Save 비활성화(로컬 세이브로 동작)


---


## 4. Storage Model


- Slot 0(`main`) 기본
- SavePayload의 `payload` 문자열 저장
- `checksum`은 SHA-256 필수(암호화 사용 시 ciphertext 기준)


---


## 5. Unity Integration


- Google Play Games Plugin for Unity(또는 Reflection 기반 접근)
- `DEV_CLOUDSAVE_GOOGLE` define ([03-ssot](../03-ssot/SKILL.md) 참고)
- Android 빌드 타깃


권장 전략(기본):
- Reflection 기반 접근으로 플러그인 미설치 환경에서도 컴파일 보장


---


## 6. Implementation Checklist


### 6.1 Sign-in
- 로그인 실패/거부 시: `AuthRequired`로 반환하고 Local Save로 진행


### 6.2 Open / Read / Commit
- 기본 플로우: `OpenWithAutomaticConflictResolution` → `ReadBinaryData` → `CommitUpdate`
- 충돌 해결: GPGS `OpenWithAutomaticConflictResolution` + `UseLongestPlaytime` (플랫폼 자동 해결)
- 애플리케이션 수준 충돌 해결은 서비스 레이어 책임(프레임워크 범위 밖)


### 6.3 Failure Handling
- 네트워크 실패: `TemporaryFailure`
- 플러그인 미설치/비지원: `NotAvailable`
- 데이터 파싱 실패: `FatalFailure`


---


## 7. QA Scenarios


- 로그인 성공/실패/거부 각각 Save/Load 동작 확인
- 오프라인에서 로컬 진행 → 온라인 복귀 후 업로드 재시도 **(서비스 레이어)**
- 2기기 충돌: GPGS `UseLongestPlaytime` 자동 해결 확인
- payload UTF-8 JSON 왕복 확인


---


## Implementation Reference


| Item | Path (UPM) |
|------|-----------|
| SaveCloudClientGoogle | `Runtime/Unity/SaveCloud/SaveCloudClientGoogle.cs` |
| ISaveCloudClient | `Runtime/Unity/SaveCloud/ISaveCloudClient.cs` |
| SaveCloudManager | `Runtime/Unity/SaveCloud/SaveCloudManager.cs` |

- Platform guard: `#if UNITY_ANDROID && !UNITY_EDITOR`
- Reflection-based: GPGS 플러그인 미설치 환경에서도 컴파일 보장
- `_mapStatus`: `Convert.ToInt32(status)` 사용 (boxed enum 안전)

UPM root: `framework-cs/upm/com.devian.foundation/`
