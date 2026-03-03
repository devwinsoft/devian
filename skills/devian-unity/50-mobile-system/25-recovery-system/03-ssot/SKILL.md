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

```
[version: 1 byte][payload: ComplexUtil obfuscated Base64 string]
```

- `version`: DVN 포맷 버전 (현재 `0x01`).
  - 디코딩 시 version 바이트로 파이프라인을 분기한다.
  - 지원하지 않는 version이면 에러를 반환한다.
- `payload`: ComplexUtil.Encrypt_Base64로 난독화된 JSON 문자열.


---


## B) Encoding Pipeline (정본)

SaveDataManager의 payload 포맷과 동일한 ComplexUtil 난독화를 사용한다.
기기별 암호화(SaveLocalDeviceKeyStore AES-GCM)는 디스크 I/O 레이어에만 존재하며,
DVN 파이프라인에는 포함되지 않는다.

### Encode (Export: JSON → .dvn)

```
단계      입력            처리                          출력
────────────────────────────────────────────────────────────
1         string (JSON)   ComplexUtil.Encrypt_Base64    string (obfuscated)
2         string          version prefix               string (.dvn content)
```

### Decode (Import: .dvn → JSON)

```
단계      입력              처리                          출력
────────────────────────────────────────────────────────────
1         string (.dvn)     version parse                string (payload)
2         string            ComplexUtil.Decrypt_Base64   string (JSON)
```

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
- SaveData JSON의 `version`(현재 10)과 DVN 포맷 `version`(현재 1)은 별개다.
  - DVN version: 인코딩 파이프라인 버전
  - JSON version: 게임 상태 스키마 버전 (SaveDataJsonCodec 관할)
