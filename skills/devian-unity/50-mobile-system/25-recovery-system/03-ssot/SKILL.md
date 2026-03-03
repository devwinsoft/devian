# 03-ssot — 25-recovery-system (통합 SSOT)


Status: ACTIVE
AppliesTo: v10


## 이 문서가 정본이다 (SSOT)

Recovery 관련 규칙의 단일 SSOT는 이 문서다.

- DVN 파일 포맷 스펙
- 인코딩/디코딩 파이프라인 정의
- 파일 확장자 및 MIME type

비정본(이 문서에서 다루지 않음):
- SaveData JSON 스키마 — [43-savedata-json-codec](../../21-savedata-system/43-savedata-json-codec/SKILL.md)의 정본을 참조한다.
- 운영툴 — `devian/80-tools` 도메인의 정본을 참조한다 (추후).


---


## A) DVN File Format (정본)

`.dvn` 파일은 버전 헤더 + 난독화된 payload로 구성된다.

### v1 (레거시)

```
[0x01][ComplexUtil.Encrypt_Base64(json)]
```

### v2 (현재)

```
[0x02][ComplexUtil.Encrypt_Base64(json + "\n" + hmac_hex_64)]
```

- `version`: DVN 포맷 버전.
  - `0x01`: HMAC 없음 (레거시). Decode 시 하위호환으로 허용하되 무결성 미검증.
  - `0x02`: HMAC 포함 (현재). Encode는 항상 v2로 생성한다.
  - 디코딩 시 version 바이트로 파이프라인을 분기한다.
  - 지원하지 않는 version이면 에러를 반환한다.
- `payload` (v1): ComplexUtil.Encrypt_Base64(json).
- `payload` (v2): ComplexUtil.Encrypt_Base64(json + "\n" + hmac_hex_64).
  - `"\n"`: 구분자. compact JSON에는 literal newline이 없으므로 안전.
  - `hmac_hex_64`: HMAC-SHA256 결과 (64-char lowercase hex). §E 참조.


---


## B) Encoding Pipeline (정본)

SaveDataManager의 payload 포맷과 동일한 ComplexUtil 난독화를 사용한다.
기기별 암호화(SaveLocalDeviceKeyStore AES-GCM)는 디스크 I/O 레이어에만 존재하며,
DVN 파이프라인에는 포함되지 않는다.

### Encode (Export: JSON → .dvn) — v2

```
단계      입력            처리                                    출력
──────────────────────────────────────────────────────────────────────
1         string (JSON)   socialUserId 추출 + HMAC 생성 (§E)     string (hmac_hex_64)
2         json + hmac     json + "\n" + hmac → signedPayload     string (signedPayload)
3         signedPayload   ComplexUtil.Encrypt_Base64              string (obfuscated)
4         obfuscated      (char)0x02 prefix                      string (.dvn content)
```

Encode는 항상 v2를 생성한다.

### Decode (Import: .dvn → JSON) — v2

```
단계      입력              처리                                    출력
──────────────────────────────────────────────────────────────────────
1         string (.dvn)     version parse (0x02)                    string (obfuscated)
2         obfuscated        ComplexUtil.Decrypt_Base64              string (signedPayload)
3         signedPayload     split: json + hmac 분리                string (json), string (hmac)
4         json + hmac       HMAC 검증 (§E)                         pass / RECOVERY_HMAC_FAILED
5         json              return                                  string (JSON)
```

### Decode (Import: .dvn → JSON) — v1 (하위호환)

```
단계      입력              처리                                    출력
──────────────────────────────────────────────────────────────────────
1         string (.dvn)     version parse (0x01)                    string (obfuscated)
2         obfuscated        ComplexUtil.Decrypt_Base64              string (JSON)
```

v1은 HMAC 없이 디코딩된다 (레거시 하위호환).

- 각 단계에서 실패 시 즉시 에러를 반환한다 (silent fail 금지).

> ComplexUtil 정본: [31-variable-complex](../../../../devian/10-module/20-core/31-variable-complex/SKILL.md)


---


## C) File Identity (정본)

| 항목 | 값 |
|------|-----|
| 확장자 | `.dvn` |
| MIME type | `application/octet-stream` |
| UTI (iOS) | `com.devian.savedata` |
| pathPattern (Android) | `.*\\.dvn` |

- 커스텀 UTI 및 intent-filter 설정은 [30-recovery-platform](../30-recovery-platform/SKILL.md) 참조.


---


## D) Version Migration (정본)

- DVN 포맷 변경 시 version 바이트를 증가시킨다.
- 이전 version의 .dvn 파일은 해당 version의 디코딩 로직으로 처리한다.
- SaveData JSON의 `version`(현재 10)과 DVN 포맷 `version`(현재 2)은 별개다.
  - DVN version: 인코딩 파이프라인 버전
  - JSON version: 게임 상태 스키마 버전 (SaveDataJsonCodec 관할)

| DVN version | 설명 | Encode | Decode |
|-------------|------|--------|--------|
| 0x01 | 레거시 (HMAC 없음) | ❌ 생성 안 함 | ✅ 하위호환 허용 |
| 0x02 | 현재 (HMAC 포함) | ✅ 항상 이 버전 | ✅ HMAC 검증 |


---


## E) HMAC Integrity (정본)

.dvn 파일의 무결성을 보장하기 위해 HMAC-SHA256을 사용한다.

| 항목 | 값 |
|------|-----|
| 알고리즘 | HMAC-SHA256 |
| 출력 | 64-char lowercase hex string |
| HMAC 키 | `APP_SECRET + socialUserId` (문자열 연결) |
| HMAC 대상 | json (compact JSON string) |
| socialUserId 출처 | json 내부 `account.socialUserId` 필드 |
| 구분자 | `"\n"` (json + "\n" + hmac_hex) |
| APP_SECRET | C#/TS 양쪽 동일 값. 코드 내 상수. |

### HMAC 키 구성

```
key = UTF-8.encode(APP_SECRET + socialUserId)
```

- `socialUserId`가 null/empty인 경우 (GUEST, EDITOR): 빈 문자열로 처리
- APP_SECRET은 C# (`RecoveryCodec`)과 TS (`dvn-codec`) 양쪽에 동일 값으로 정의한다

### signedPayload 구조

```
signedPayload = json + "\n" + hmac_hex_64
```

- json: compact JSON (개행 없음, JSON.stringify / ToJson 출력 그대로)
- hmac_hex_64: HMAC-SHA256 결과의 lowercase hex (항상 64자)
- 분리 방법: 마지막 65번째 문자가 `"\n"`, 이후 64자가 hmac

### 검증 실패 시

- HMAC 불일치 → `RECOVERY_HMAC_FAILED` 에러 반환
- Import 시 추가로 json 내부 `account.socialUserId`와 현재 로그인된 socialUserId를 비교한다 (RecoveryManager 관할)
