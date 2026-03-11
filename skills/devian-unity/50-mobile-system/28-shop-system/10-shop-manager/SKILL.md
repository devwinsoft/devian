name: 10-shop-manager
description: MobileSystem ShopManager를 CompoSingleton으로 구현하고 BuyAsync(productId) 구매 플로우(통화 차감, ADS 시청, 구매 제한, amount 배수 보상, 실패 시 롤백)를 적용할 때 사용한다.
---

# 10-shop-manager

Status: ACTIVE
AppliesTo: v10

ShopManager는 MobileSystem의 인게임 상점 구매 진입점이다.

---

## 1. Class

```csharp
public sealed class ShopManager : CompoSingleton<ShopManager>
```

- namespace: `Devian`
- asmdef: `Devian.Samples.MobileSystem`
- MobileApplication에서 `RequireComponent(typeof(ShopManager))`로 보장한다.

---

## 2. Public API

```csharp
public bool CanBuy(string productId)
public Task<CommonResult<RewardData[]>> BuyAsync(string productId, CancellationToken ct = default)
```

구매 플로우:

1. `ShopProduct.TryGet(productId)`로 상품 조회
2. 구매 제한(`maxCount/resetDays`) 검사 (서버 시간 기준)
3. 결제 타입 분기
  - `FREE`: 차감 없음
  - `ADS`: `AdsManager.ShowAsync()` 성공 필요
  - 기타 통화: 잔고 검증 + 가격 차감
4. `RewardManager.Instance.ApplyRewardGroup(shopProduct.RewardGroupId, amount)` 보상 지급
5. 지급 실패 시 차감 롤백
6. 성공 시 제한 카운트 증가 + `AppliedRewards` 반환

---

## 3. Currency Deduction Rule

- 일반 통화: `wallet.Get(currencyType) >= price` 검증 후 `wallet.TryAdd(currencyType, -price)`
- `CURRENCY_TYPE.FREE`: 가격 0 전용(차감 없음)
- `CURRENCY_TYPE.ADS`: `AdsManager.ShowAsync()` 성공 시 구매 성공
- `CURRENCY_TYPE.JEWEL`:
  - 구매 가능 조건: `JEWEL_FREE + JEWEL_PAID >= price`
  - 차감 우선순위: `JEWEL_FREE` 먼저, 부족분은 `JEWEL_PAID`

예:
- free=70, paid=50, price=100 → free 70 차감 + paid 30 차감

---

## 4. Hard Rules

- `SHOP_PRODUCT.rewardGroupId`가 비어 있으면 구매 실패.
- `SHOP_PRODUCT.amount`는 최소 1로 보정한다.
- 구매 제한 시간 계산은 `RemoteConfigManager` 서버 시간(`serverNowUtcMs`)만 사용한다.
- 가격 차감 성공 후 보상 지급 실패 시 반드시 롤백.
- `TB_SHOP_PRODUCT`/`TB_REWARD`를 직접 조회하는 책임 분리:
  - 상품 조회: ShopProduct
  - 보상 지급: RewardManager

---

## 5. Implementation Location (3-path mirror)

- UPM (정본): `framework-cs/upm/com.devian.samples/Samples~/MobileSystem/Runtime/Shop/ShopManager.cs`
- Packages (sync): `framework-cs/apps/UnityExample/Packages/com.devian.samples/Samples~/MobileSystem/Runtime/Shop/ShopManager.cs`
- Assets/Samples (import): `framework-cs/apps/UnityExample/Assets/Samples/Devian Samples/{version}/MobileSystem/Runtime/Shop/ShopManager.cs`

---

## 6. Related

- [11-shop-product](../11-shop-product/SKILL.md)
- [12-shop-storage](../12-shop-storage/SKILL.md)
- [49-reward-system/10-reward-manager](../../49-reward-system/10-reward-manager/SKILL.md)
- [22-inventory-system/12-inventory-wallet](../../22-inventory-system/12-inventory-wallet/SKILL.md)
- [50-mobile-system/01-policy](../../01-policy/SKILL.md)
