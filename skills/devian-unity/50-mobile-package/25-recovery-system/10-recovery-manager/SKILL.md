# 10-recovery-manager


RecoveryManager는 save data의 **Export(전송)** 와 **Import(복원)** 를 오케스트레이션한다.

RecoveryManager는 **독립 클래스**이다.
- SaveDataManager의 public API만 사용한다 (internal 접근 금지).
- 인코딩/디코딩은 RecoveryCodec에 위임한다.
- 서버/네트워크에 의존하지 않는다.


---


## Singleton

```csharp
CompoSingleton<RecoveryManager>.Instance
```

- Registry key: `RecoveryManager`
- 다른 매니저에서 접근: `Singleton.Get<RecoveryManager>()`


---


## Responsibilities (정본)

- Export: 현재 save data → .dvn 파일 생성 → DevianShare로 공유
- Import (경로 A): OS 파일 연결로 .dvn 수신 → 디코딩 → 검증 → SaveDataManager로 복원
- Import (경로 B): 앱 내 File Picker로 .dvn 선택 → 디코딩 → 검증 → SaveDataManager로 복원

비책임(금지):
- 자동 복구
- 클라우드 동기화
- 서버/네트워크 호출
- 복원 이력(ledger) 관리


---


## Dependencies (개념)

- SaveDataManager — 평문 JSON 획득 및 복원 위임
- RecoveryCodec — .dvn 인코딩/디코딩 + HMAC 무결성 검증
- AccountManager — 현재 로그인된 socialUserId 획득 (Import 시 계정 검증)
- DevianShare 네이티브 플러그인 — OS 공유 시트 호출 (Export), 파일 선택 다이얼로그 (Import 경로 B) ([30-recovery-platform](../30-recovery-platform/SKILL.md) 참조)
- Platform Native Layer — .dvn 파일 수신 (Import 경로 A, [30-recovery-platform](../30-recovery-platform/SKILL.md) 참조)


---


## Public API


### Export (공유 시트)

- `Task<CommonResult<bool>> ExportDvnAsync(CancellationToken ct)`

```
1. SaveDataManager.Instance.ToJson() → 평문 JSON 획득
2. RecoveryCodec.Encode(json) → .dvn string (v2: HMAC 포함)
3. 임시 파일 저장 (Application.temporaryCachePath)
   파일명: recovery_{timestamp}.dvn
4. DevianShare.ShareFile로 OS 공유 시트 열기
   - Subject: 앱 이름 + "Save Data Recovery"
   - 첨부: .dvn 파일
```


### Export (이메일 전용)

- `Task<CommonResult<bool>> ExportDvnViaEmailAsync(string recipient, CancellationToken ct)`

```
1~3. ExportDvnAsync와 동일 (PrepareDvnFileAsync 공유, v2 HMAC 포함)
4. DevianShare.SendEmail로 이메일 앱 열기
   - 수신자: recipient (프리셋)
   - Subject: 앱 이름 + "Save Data Recovery"
   - 첨부: .dvn 파일
```

- 이메일 앱만 표시된다 (범용 공유 시트 아님).
- iOS: `MFMailComposeViewController`, Android: `message/rfc822` + `EXTRA_EMAIL`


### Import (경로 A: OS 파일 연결)

- `Task<CommonResult<bool>> ImportDvnAsync(string filePath, CancellationToken ct)`

```
1. filePath에서 .dvn 파일 읽기
2. RecoveryCodec.Decode(content) → CommonResult<string> 평문 JSON
   - v2: HMAC 무결성 검증 포함 (RecoveryCodec 내부)
   - v1: 하위호환 (HMAC 없이 디코딩)
3. JSON 기본 검증 (파싱 가능 여부)
4. socialUserId 일치 검증:
   json 내부 account.socialUserId vs AccountManager.Instance.Storage.socialUserId
   - 불일치 → 에러 반환 (다른 계정의 데이터)
5. SaveDataManager.Instance.RestoreFromPlainJsonAsync(json, true, ct)
   → 런타임 적용 + local/cloud 영속화
6. 임시 .dvn 파일 삭제
```


### Import (경로 B: 앱 내 File Picker)

- `Task<CommonResult<bool>> PickAndImportDvnAsync(CancellationToken ct)`

```
1. 네이티브 파일 선택 다이얼로그 열기 (DevianShare.PickFile)
   - Android: ACTION_OPEN_DOCUMENT (application/octet-stream, 기본 폴더: Downloads)
   - iOS: UIDocumentPickerViewController (public.data UTI, 기본 폴더: Downloads)
   - TaskCompletionSource로 네이티브 콜백을 async/await 변환
   - iOS/Android 디바이스 빌드에서만 동작. Editor/기타 플랫폼에서는 skip + 성공 반환.
2. 사용자가 파일 선택 → 네이티브가 임시 경로로 복사 → OnFilePickerResult 콜백
   - 취소 시: 에러 반환 (사용자 취소, 정상 플로우)
3. ImportDvnAsync(filePath, ct) 호출 (기존 로직 재사용)
```

- 기존 `ImportDvnAsync`를 재사용하므로, 디코딩/검증/복원 로직은 동일하다.
- File Picker에서 .dvn 이외 파일을 선택해도, Decode 단계에서 실패로 처리된다.
- 네이티브 구현: [30-recovery-platform](../30-recovery-platform/SKILL.md) §Import — DevianFilePicker 참조


---


## SaveDataManager API 연결

RecoveryManager는 SaveDataManager의 아래 public API를 사용한다:

- **Export**: `string ToJson()` — 현재 런타임 게임 상태를 평문 JSON으로 직렬화
- **Import**: `Task<CommonResult<bool>> RestoreFromPlainJsonAsync(string json, bool saveCloud, CancellationToken ct)` — 평문 JSON 복원 + 영속화

정본: [10-savedata-manager](../../21-savedata-system/10-savedata-manager/SKILL.md) §Public API


---


## Error Cases

| 상황 | 처리 |
|------|------|
| 공유 시트 호출 실패 | `COMMON_ERROR_TYPE` 에러 반환 |
| 파일 선택 취소 (File Picker) | 에러 반환 (사용자 취소, 정상 플로우) |
| .dvn 파일 읽기 실패 | 에러 반환 + 유저에게 안내 |
| RecoveryCodec.Decode 실패 (손상/위조) | 에러 반환 + "복원 실패" 안내 |
| HMAC 불일치 (v2, RecoveryCodec 내부) | `RECOVERY_HMAC_FAILED` 에러 반환 |
| socialUserId 불일치 (다른 계정) | 에러 반환 + "다른 계정의 데이터" 안내 |
| JSON version 불일치 | 에러 반환 + 버전 안내 |
| SaveDataManager 복원 실패 | 에러 반환, 기존 데이터 유지 |


---


## Sequence Example

### Export (공유 시트)
1. 유저가 설정 화면에서 "데이터 전송" 버튼 탭
2. `RecoveryManager.ExportDvnAsync(ct)` 호출
3. 평문 JSON 획득 → RecoveryCodec.Encode → .dvn 파일 생성 → DevianShare.ShareFile
4. 유저가 이메일 앱 선택 → 개발자에게 전송

### Export (이메일 전용)
1. 유저가 설정 화면에서 "이메일로 전송" 버튼 탭
2. `RecoveryManager.ExportDvnViaEmailAsync(recipient, ct)` 호출
3. 평문 JSON 획득 → RecoveryCodec.Encode → .dvn 파일 생성 → DevianShare.SendEmail
4. 이메일 앱이 수신자 프리셋 상태로 열림 → 유저가 전송

### Import (경로 A: OS 파일 연결)
1. 유저가 이메일에서 .dvn 첨부파일 탭
2. OS가 게임으로 파일 전달 (Custom File Type Association)
3. 네이티브 레이어 → `RecoveryManager.ImportDvnAsync(filePath, ct)` 호출
4. 디코딩 (v2: HMAC 검증) → socialUserId 일치 확인 → 복원 → 유저에게 "복원 완료" 안내

### Import (경로 B: 앱 내 File Picker)
1. 유저가 설정 화면에서 "데이터 복원" 버튼 탭
2. `RecoveryManager.PickAndImportDvnAsync(ct)` 호출
3. 네이티브 파일 선택 다이얼로그 표시 (SAF / UIDocumentPicker)
4. 유저가 .dvn 파일 선택
5. 네이티브 → 임시 경로 복사 → `OnFilePickerResult(filePath)` 콜백
6. `ImportDvnAsync(filePath, ct)` → 디코딩 → 검증 → 복원 → 유저에게 "복원 완료" 안내


---


## Implementation Location (3-path mirror)

> 3-path mirror 정책: [devian-unity/04-package-policy](../../../04-package-policy/SKILL.md), [devian-unity/01-policy](../../../01-policy/SKILL.md) §SSOT 원칙

- RecoveryManager:
  - UPM (정본): `framework-cs/upm/com.devian.samples/Samples~/MobilePackage/Runtime/Recovery/RecoveryManager.cs`
  - Packages (sync): `framework-cs/apps/UnityExample/Packages/com.devian.samples/Samples~/MobilePackage/Runtime/Recovery/RecoveryManager.cs`
  - Assets/Samples (import): `framework-cs/apps/UnityExample/Assets/Samples/Devian Samples/{version}/MobilePackage/Runtime/Recovery/RecoveryManager.cs`

asmdef:
- `Devian.Samples.MobilePackage.asmdef`


---


## Related

- [25-recovery-system/03-ssot](../03-ssot/SKILL.md) — DVN 포맷 정본
- [20-recovery-codec](../20-recovery-codec/SKILL.md) — RecoveryCodec (인코딩/디코딩)
- [30-recovery-platform](../30-recovery-platform/SKILL.md) — 플랫폼 파일 수신부 + 공유 시트 + 파일 선택
- [10-savedata-manager](../../21-savedata-system/10-savedata-manager/SKILL.md) — SaveDataManager 진입점
