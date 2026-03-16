# 20-recovery-codec


RecoveryCodec는 평문 JSON과 `.dvn` 파일 내용 간의 **인코딩/디코딩 파이프라인**을 구현하는 static utility 클래스다.

- 파이프라인 정의는 [03-ssot](../03-ssot/SKILL.md) §B를 따른다.
- HMAC 무결성 검증은 [03-ssot](../03-ssot/SKILL.md) §E를 따른다.
- SaveDataManager의 payload 포맷과 동일한 ComplexUtil 난독화를 사용한다.


---


## Class Design

```csharp
internal static class RecoveryCodec
```

- static utility (인스턴스 불필요).
- RecoveryManager에서만 호출한다.
- 외부 매니저가 직접 호출하지 않는다.


---


## Public API


### Encode

```csharp
/// <summary>
/// 평문 JSON → .dvn 파일 내용 (v2: version header + obfuscated signed payload)
/// json 내부 account.socialUserId를 추출하여 HMAC 키에 사용한다.
/// </summary>
internal static string Encode(string json)
```

파이프라인 (v2):
```
1. json에서 account.socialUserId 추출
2. HMAC-SHA256(json, APP_SECRET + socialUserId) → hmac_hex_64
3. signedPayload = json + "\n" + hmac_hex_64
4. ComplexUtil.Encrypt_Base64(signedPayload) → obfuscated string
5. (char)0x02 + obfuscated string → final string
```


### Decode

```csharp
/// <summary>
/// .dvn 파일 내용 → 평문 JSON
/// v2: HMAC 무결성 검증 포함.
/// v1: 하위호환 (HMAC 없이 디코딩).
/// 실패 시 CommonResult 에러 반환.
/// </summary>
internal static CommonResult<string> Decode(string dvnContent)
```

파이프라인 (v2, version = 0x02):
```
1. version byte 파싱 (첫 바이트) → 0x02
2. ComplexUtil.Decrypt_Base64(payload) → signedPayload
3. signedPayload 분리: json + "\n" + hmac_hex_64
4. json에서 account.socialUserId 추출
5. HMAC 재계산 및 검증 → 불일치 시 RECOVERY_HMAC_FAILED
6. Return json
```

파이프라인 (v1 하위호환, version = 0x01):
```
1. version byte 파싱 → 0x01
2. ComplexUtil.Decrypt_Base64(payload) → json (HMAC 검증 없음)
3. Return json
```


---


## Version Header

```
offset  size  description
──────────────────────────
0       1     DVN format version (0x01 or 0x02)
1       N     ComplexUtil obfuscated Base64 payload
```

- version은 ASCII 문자가 아닌 raw byte다.
- `0x01`: 레거시 (HMAC 없음). Decode만 허용.
- `0x02`: 현재 (HMAC 포함). Encode는 항상 0x02.
- Encode 시 `(char)0x02 + obfuscatedPayload`로 조합한다.
- Decode 시 `dvnContent[0]`으로 version을 읽고 분기한다.


---


## Error Types

| 상황 | 에러 |
|------|------|
| version 미지원 | `RECOVERY_UNSUPPORTED_VERSION` |
| ComplexUtil 디코딩 실패 | `RECOVERY_DECODE_FAILED` |
| HMAC 불일치 (v2) | `RECOVERY_HMAC_FAILED` |

에러 타입의 정확한 이름/위치는 구현 시 `COMMON_ERROR_TYPE` 패턴을 따른다.


---


## Implementation Location (3-path mirror)

> 3-path mirror 정책: [devian-unity/07-samples-creation-guide](../../../07-samples-creation-guide/SKILL.md), [devian-unity/01-policy](../../../01-policy/SKILL.md) §SSOT 원칙

- RecoveryCodec:
  - UPM (정본): `framework-cs/upm/com.devian.samples/Samples~/MobilePackage/Runtime/Recovery/RecoveryCodec.cs`
  - Packages (sync): `framework-cs/apps/UnityExample/Packages/com.devian.samples/Samples~/MobilePackage/Runtime/Recovery/RecoveryCodec.cs`
  - Assets/Samples (import): `framework-cs/apps/UnityExample/Assets/Samples/Devian Samples/{version}/MobilePackage/Runtime/Recovery/RecoveryCodec.cs`

asmdef:
- `Devian.Samples.MobilePackage.asmdef`


---


## Related

- [03-ssot](../03-ssot/SKILL.md) — DVN 포맷 정본 (§E HMAC Integrity)
- [10-recovery-manager](../10-recovery-manager/SKILL.md) — RecoveryManager (호출자)
- [31-variable-complex](../../../../devian/10-module/20-core/31-variable-complex/SKILL.md) — ComplexUtil 정본
