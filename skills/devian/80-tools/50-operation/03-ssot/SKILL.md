# 03-ssot — 50-operation


Status: ACTIVE
AppliesTo: v10
ParentSSOT: skills/devian/80-tools/03-ssot/SKILL.md


## Scope

Operation 웹앱의 **기능(탭) 정의**를 관리한다.
각 탭의 입출력, 파이프라인, 참조 SSOT를 이 문서에서 정의한다.


---


## A) Obfuscate

ComplexUtil byte-substitution. 평문 → 난독화.

| 항목 | 값 |
|------|-----|
| 입력 | 평문 바이트 |
| 출력 | 난독화된 바이트 |
| 버튼 | [Obfuscate] |

> ComplexUtil 정본: [31-variable-complex](../../../../10-module/20-core/31-variable-complex/SKILL.md)


---


## B) Deobfuscate

ComplexUtil byte-substitution. 난독화 → 평문.

| 항목 | 값 |
|------|-----|
| 입력 | 난독화된 바이트 |
| 출력 | 평문 바이트 |
| 버튼 | [Deobfuscate] |

> ComplexUtil 정본: [31-variable-complex](../../../../10-module/20-core/31-variable-complex/SKILL.md)


---


## C) Save Data

Save Data 편집 워크플로우. 파일 확장자(`.json` / `.dvn`)에 따라 자동 분기.


### 입력 형식

| 확장자 | 출처 | 외부 래핑 |
|--------|------|-----------|
| `.json` | Unity Editor 로컬 저장 | 없음 (SaveLocalPayload JSON 그대로) |
| `.dvn` | 모바일 Export | DVN 인코딩 (version byte + ComplexUtil) |

두 형식 모두 내부 `payload` 필드는 `ComplexUtil.Encrypt_Base64(게임 상태 JSON)`.


### 양방향 프로세스

```
[SaveDataManager]                    [Operation]
  ExportDvnAsync  ── .dvn ──→  Import (decode)
  로컬 저장        ── .json ──→  Import (parse)
                                   ↓ JSON 편집
  ImportDvnAsync  ←── .dvn ──  Export (encode)
  RestoreFromPlainJsonAsync ←── .json ──  Export (serialize)
```

### DVN 파이프라인

```
Export (.dvn): JSON → ComplexUtil.Encrypt_Base64 → version prefix → .dvn
Import (.dvn): .dvn → version parse → ComplexUtil.Decrypt_Base64 → JSON
```

정본: [25-recovery-system/03-ssot](../../../../devian-unity/50-mobile-system/25-recovery-system/03-ssot/SKILL.md)
- DVN 포맷: §A
- 인코딩 파이프라인: §B


---


## Related

- [00-overview](../00-overview/SKILL.md) — Operation 개요
- [01-policy](../01-policy/SKILL.md) — 정책
- [Tools SSOT](../../03-ssot/SKILL.md) — 상위 SSOT
