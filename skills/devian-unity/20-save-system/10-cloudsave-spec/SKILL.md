# 20-save-system — Cloud Save Spec (Base)


Status: ACTIVE
AppliesTo: v10


---


## 1. Save Payload (Base)


저장 데이터는 플랫폼별 구현과 무관하게 아래 논리 스키마를 따른다.


- `version: int` — 세이브 스키마 버전
- `updateTime: string` — `"yyyyMMdd:HHmmss"` (DateTime.Now, device local time)
- `utcTime: long` — Unix epoch milliseconds (UTC)
- `payload: string` — JSON(권장) 또는 압축된 텍스트
- `checksum: string` — SHA-256
  - Devian `CloudSaveManager` 사용 시: 저장 전에 checksum을 생성하고, 로드 시 checksum을 검증한다.
  - 호환성/이식성을 위해 payload 객체 자체는 checksum이 null/empty일 수 있다(구현 참조).


권장:
- payload는 작게(수 KB~수십 KB).
- 큰 바이너리/리플레이/로그는 Cloud Save에 넣지 않는다.

> 구현 참조: `CloudSavePayload.cs` (ctor `checksum = null`), `CloudSaveManager.cs` (Save: 항상 생성, Load: `IsNullOrEmpty` 시 검증 생략)


---


## 2. Slot Policy


> 슬롯 구성/이름/개수는 **서비스 레이어(개발자) 책임**이다.
> 프레임워크는 slot allowlist(매핑) 기반으로 동작하며, 미설정 시 실패 처리한다(암묵적 기본값 없음).


권장(서비스 레이어 예시):
- `main`, (선택) `backup`, `manual`

> 구현 참조: `CloudSaveManager.cs` (`TryResolveCloudSlot` — 매핑 미발견 시 `Failure("cloudsave.slot.missing")`), `LocalSaveManager.cs` (`TryResolveFilename` — 매핑 미발견 시 `Failure("localsave.slot.missing")`)


---


## 3. Save Policy — Guidance (Non-normative)


> 아래는 **서비스 레이어 책임**이며 프레임워크가 강제하지 않는다.
> 프레임워크는 `SaveAsync` / `Save`를 제공할 뿐, 호출 시점·빈도는 서비스가 결정한다.


권장 패턴(서비스 레이어):
- 이벤트 기반 저장(스테이지 클리어/보상 확정/설정 변경/백그라운드 진입)
- 디바운스(짧은 시간 연속 변경 → 1회 저장)
- 변경 없으면 저장 생략(해시 비교)


---


## 4. Conflict Resolution — Guidance (Non-normative)


> 아래는 **서비스 레이어 책임**이며 프레임워크 범위 밖이다.
> GPGS 구현은 `OpenWithAutomaticConflictResolution` + `UseLongestPlaytime`로
> 플랫폼 수준 자동 충돌 해결을 사용한다(코드 참조: `GooglePlayGamesCloudSaveClient`).


권장 패턴(서비스 레이어):
- `utcTime` 기준 최신 승리
- 단조 증가 값(최고 스테이지/업적 등)은 max 병합(선택)
- 재화/경제 데이터는 병합 금지(복제/치팅 리스크)


---


## 5. Failure Handling — Guidance (Non-normative)


> 프레임워크는 실패 시 `CoreResult.Failure`를 반환할 뿐이다.
> 재시도·fallback 정책은 **서비스 레이어 책임**이다.


권장 패턴(서비스 레이어):
- 오프라인: 로컬 세이브 유지
- 업로드 실패: 다음 기회에 재시도
- 다운로드 실패: 마지막 로컬 세이브로 실행 가능해야 함


---


## 6. Integrity / Encryption


- `checksum`은 **SHA-256**으로 계산한다.
- 암호화/복호화는 Devian `Crypto` 유틸리티를 사용한다.
  - Implementation Ref: `framework-cs/upm/com.devian.foundation/Runtime/Unity/LocalSave/Crypto.cs`
- 암호화 적용 순서: `encrypt(payload) -> checksum(ciphertext)`


### Encryption Key/IV


- `CloudSaveManager`는 AES key/IV를 **base64 문자열**로 취급한다.
- 내부 저장 필드는 `CString`이며(Inspector 기본값/편의 목적), 실제 base64 문자열은 `CString.Value`로 사용된다.
- 키/IV의 **저장 위치/보관 정책**은 서비스 레이어(개발자) 책임이다.
  - 프레임워크는 `GetKeyIvBase64 / SetKeyIvBase64 / ClearKeyIv`로 **내보내기/주입 수단만 제공**한다.


---


## 7. Local Save Integration (Unity)


Local Save와 Cloud Save는 **동일한 논리 필드(버전/시간/payload/checksum)**를 사용한다.
단, 직렬화 필드명/키는 구현에 따라 다를 수 있다(구현 참조).
슬롯/파일명 매핑은 **서비스 레이어에서 구성**하며, 프레임워크는 암묵적 기본값을 제공하지 않는다.

- 파일명은 **슬롯 → 파일명 매핑**을 통해 변경 가능해야 한다.
- 저장 경로는 **LocalSaveManager에서 설정**할 수 있어야 한다.

> 구현 참조: `CloudSavePayload.cs` (PascalCase: `Version`, `Payload`), `LocalSavePayload.cs` (camelCase: `version`, `payload`), `CloudSaveManager.cs` / `LocalSaveManager.cs` (슬롯 매핑)


---


## Implementation Reference


### Cloud Save

| Item | Path (UPM) |
|------|-----------|
| CloudSavePayload | `Runtime/Unity/CloudSave/CloudSavePayload.cs` |
| CloudSaveResult | `Runtime/Unity/CloudSave/CloudSaveResult.cs` |
| ICloudSaveClient | `Runtime/Unity/CloudSave/ICloudSaveClient.cs` |
| CloudSaveManager | `Runtime/Unity/CloudSave/CloudSaveManager.cs` |
| CloudSaveCrypto | `Runtime/Unity/CloudSave/CloudSaveCrypto.cs` |


### Local Save

| Item | Path (UPM) |
|------|-----------|
| LocalSavePayload | `Runtime/Unity/LocalSave/LocalSavePayload.cs` |
| LocalSaveManager | `Runtime/Unity/LocalSave/LocalSaveManager.cs` |
| LocalSaveCrypto | `Runtime/Unity/LocalSave/LocalSaveCrypto.cs` |
| LocalSaveFileStore | `Runtime/Unity/LocalSave/LocalSaveFileStore.cs` |


UPM root: `framework-cs/upm/com.devian.foundation/`
Mirror: `framework-cs/apps/UnityExample/Packages/com.devian.foundation/`
