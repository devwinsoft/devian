# 14-purchase-settings

Status: ACTIVE
AppliesTo: v13

## SSOT

이 문서는 `PurchaseSettings` ScriptableObject의 **필드, 경로, PurchaseManager 연동** 규칙을 정의한다.

---

## 목적/범위

PurchaseManager에서 분리한 **인스펙터 설정 전용 ScriptableObject**.

- AES 암호화 불필요 (민감 데이터 아님)
- `Resources.Load`로 lazy 로드, `_settingsLoaded` 플래그로 중복 로드 방지
- 에셋 미존재 시 코드 내 기본값(fallback)으로 동작

---

## 소스 경로

### Implementation Location (3-path mirror)

> 3-path mirror 정책: [devian-unity/07-samples-creation-guide](../../../../devian-unity/07-samples-creation-guide/SKILL.md), [devian-unity/01-policy](../../../../devian-unity/01-policy/SKILL.md) §SSOT 원칙

| 위치 | 경로 |
|------|------|
| UPM (정본) | `framework-cs/upm/com.devian.samples/Samples~/MobilePackage/Runtime/Purchase/PurchaseSettings.cs` |
| Packages (sync) | `framework-cs/apps/UnityExample/Packages/com.devian.samples/Samples~/MobilePackage/Runtime/Purchase/PurchaseSettings.cs` |
| Assets/Samples (import) | `framework-cs/apps/UnityExample/Assets/Samples/Devian Samples/{version}/MobilePackage/Runtime/Purchase/PurchaseSettings.cs` |

### 에셋 경로

| 항목 | 경로 |
|------|------|
| Resources 경로 | `Devian/PurchaseSettings` |
| 프로젝트 에셋 | `Assets/Resources/Devian/PurchaseSettings.asset` |
| 에셋 생성 | Unity Editor → Project → Create → Devian → MobilePackage → Purchase Settings |

---

## 필드

| 필드 | 타입 | 기본값 | 설명 |
|------|------|--------|------|
| `SeasonPurchaseBlockedBeforeEndDays` | `int` | `3` | 시즌 종료 N일 전 구매 차단 |
| `MaxVerifyRecoveryRetries` | `int` | `3` | 검증 복구 최대 재시도 횟수 |
| `AlreadyOwnedRecoveryPollCount` | `int` | `50` | 이미 소유 복구 폴링 횟수 (~5s) |

---

## PurchaseManager 연동

PurchaseManager는 `ensureSettings()` 패턴으로 로드한다.

```csharp
PurchaseSettings _settings;
bool _settingsLoaded;

PurchaseSettings ensureSettings()
{
    if (!_settingsLoaded)
    {
        _settings = Resources.Load<PurchaseSettings>(PurchaseSettings.ResourcesPath);
        _settingsLoaded = true;
    }
    return _settings;
}
```

사용 예:

```csharp
// SeasonPurchaseBlockedBeforeEndDays
ensureSettings()?.SeasonPurchaseBlockedBeforeEndDays ?? 3

// MaxVerifyRecoveryRetries
ensureSettings()?.MaxVerifyRecoveryRetries ?? 3

// AlreadyOwnedRecoveryPollCount
ensureSettings()?.AlreadyOwnedRecoveryPollCount ?? 50
```

---

## DoD (검증 가능)

### PASS 조건

- [ ] `PurchaseSettings.cs` 최상단 SSOT가 이 문서를 가리킴
- [ ] 3-path mirror 동일
- [ ] `Assets/Resources/Devian/PurchaseSettings.asset` 존재, 기본값 일치
- [ ] PurchaseManager에 `[SerializeField]` 설정 필드 0건
- [ ] PurchaseManager에 설정 관련 `const` 0건

### FAIL 조건

- PurchaseManager에 설정 `[SerializeField]`/`const` 잔존
- 3-path mirror 불일치
- SSOT 주석이 다른 문서를 가리킴

---

## Related

- [10-purchase-manager](../10-purchase-manager/SKILL.md) — PurchaseManager 샘플
- [03-ssot](../03-ssot/SKILL.md) — Purchase 통합 SSOT
