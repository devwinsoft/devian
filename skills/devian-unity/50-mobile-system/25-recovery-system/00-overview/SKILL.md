# 25-recovery-system — Overview


Status: ACTIVE
AppliesTo: v10


Save data 손상 시 자동 복구가 불가능할 때, 유저가 이메일로 save data를 개발자에게 전송하고,
개발자가 운영툴로 수정/보상 후 패치된 데이터를 회신하는 **이메일 기반 수동 복원** 시스템이다.

서버 없이 동작하며, Custom File Type Association(`.dvn`)을 통해 딥링크와 동일한 UX를 제공한다.


---


## Flow

```
========= Export (Player → 개발자) =========

SaveDataManager → 평문 JSON (~100KB)
    ↓ RecoveryCodec.Encode
    ↓ ComplexUtil.Encrypt_Base64 → version prefix
.dvn 파일
    ↓ DevianShare → OS 공유 시트 → 이메일 전송

========= 운영 (개발자) =========

.dvn 수신 → 운영툴로 디코딩 → 수정/보상 → 재인코딩 → .dvn 회신

========= Import (Player ← 개발자) =========

이메일에서 .dvn 첨부파일 탭
    ↓ OS가 게임으로 전달 (Custom File Type Association)
    ↓ RecoveryCodec.Decode
    ↓ version parse → ComplexUtil.Decrypt_Base64 → 평문 JSON
    ↓ 검증 → SaveDataManager로 복원
```


---


## Start Here


| Document | Description |
|----------|-------------|
| [01-policy](../01-policy/SKILL.md) | 하드룰 (암호화, 파일 포맷, 보안, 모듈 경계) |
| [03-ssot](../03-ssot/SKILL.md) | DVN 파일 포맷 스펙, 인코딩 파이프라인 |
| [10-recovery-manager](../10-recovery-manager/SKILL.md) | RecoveryManager 오케스트레이터 (Export + Import) |
| [20-recovery-codec](../20-recovery-codec/SKILL.md) | RecoveryCodec (encode/decode 파이프라인) |
| [30-recovery-platform](../30-recovery-platform/SKILL.md) | iOS/Android 파일 타입 등록 및 네이티브 수신부 |


---


## Dependencies

- [21-savedata-system](../../21-savedata-system/00-overview/SKILL.md) — SaveDataManager (평문 JSON 획득/복원)
- DevianShare 네이티브 플러그인 — OS 공유 시트 호출 (Export 시, [30-recovery-platform](../30-recovery-platform/SKILL.md) 참조)

운영툴:
- [50-operation](../../../../devian/80-tools/50-operation/00-overview/SKILL.md) — 게임 운영 도구 (encode/decode 등)


---


## Related

- [21-savedata-system](../../21-savedata-system/00-overview/SKILL.md)
- [20-account-system](../../20-account-system/00-overview/SKILL.md)
