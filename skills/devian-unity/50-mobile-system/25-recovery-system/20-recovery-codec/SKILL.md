# 20-recovery-codec


RecoveryCodec는 평문 JSON과 `.dvn` 파일 내용 간의 **인코딩/디코딩 파이프라인**을 구현하는 static utility 클래스다.

- 파이프라인 정의는 [03-ssot](../03-ssot/SKILL.md) §B를 따른다.
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
/// 평문 JSON → .dvn 파일 내용 (version header + obfuscated payload)
/// </summary>
internal static string Encode(string json)
```

파이프라인:
```
1. ComplexUtil.Encrypt_Base64(json) → obfuscated string
2. (char)version + obfuscated string → final string
```


### Decode

```csharp
/// <summary>
/// .dvn 파일 내용 → 평문 JSON
/// 실패 시 CommonResult 에러 반환.
/// </summary>
internal static CommonResult<string> Decode(string dvnContent)
```

파이프라인:
```
1. version byte 파싱 (첫 바이트)
   - 지원하지 않는 version → 에러 반환
2. payload = dvnContent.Substring(1)
3. ComplexUtil.Decrypt_Base64(payload) → plain JSON
   - 실패 → 에러 반환 (손상/위조)
```


---


## Version Header

```
offset  size  description
──────────────────────────
0       1     DVN format version (0x01)
1       N     ComplexUtil obfuscated Base64 payload
```

- version은 ASCII 문자가 아닌 raw byte다.
- Encode 시 `(char)version + obfuscatedPayload`로 조합한다.
- Decode 시 `dvnContent[0]`으로 version을 읽고 `dvnContent.Substring(1)`로 payload를 분리한다.


---


## Error Types

| 상황 | 에러 |
|------|------|
| version 미지원 | `RECOVERY_UNSUPPORTED_VERSION` |
| ComplexUtil 디코딩 실패 | `RECOVERY_DECODE_FAILED` |

에러 타입의 정확한 이름/위치는 구현 시 `CommonErrorType` 패턴을 따른다.


---


## Implementation Location (3-path mirror)

> 3-path mirror 정책: [devian-unity/07-samples-creation-guide](../../../07-samples-creation-guide/SKILL.md), [devian-unity/03-ssot](../../../03-ssot/SKILL.md) §UPM Packages Sync

- RecoveryCodec:
  - UPM (정본): `framework-cs/upm/com.devian.samples/Samples~/MobileSystem/Runtime/Recovery/RecoveryCodec.cs`
  - Packages (sync): `framework-cs/apps/UnityExample/Packages/com.devian.samples/Samples~/MobileSystem/Runtime/Recovery/RecoveryCodec.cs`
  - Assets/Samples (import): `framework-cs/apps/UnityExample/Assets/Samples/Devian Samples/{version}/MobileSystem/Runtime/Recovery/RecoveryCodec.cs`

asmdef:
- `Devian.Samples.MobileSystem.asmdef`


---


## Related

- [03-ssot](../03-ssot/SKILL.md) — DVN 포맷 정본
- [10-recovery-manager](../10-recovery-manager/SKILL.md) — RecoveryManager (호출자)
- [31-variable-complex](../../../../devian/10-module/20-core/31-variable-complex/SKILL.md) — ComplexUtil 정본
