# 03-ssot — 28-shop-system

Status: ACTIVE
AppliesTo: v10

## SSOT Scope

이 문서는 Shop catalog의 런타임 라이프사이클 정본이다.

- catalog initialize 규칙
- catalog refresh 조건 탐색/실행 순서
- `Initialize`, `RefreshProducts`, `GetCatalog<T>()`, catalog public API의 계약
- storage 반영 순서(`autoRefreshUtcMs`, `adsRefreshUtcMs`, `manualRefreshUtcMs`, `manualRefreshRemainCount`, remain/daily 상태)
- typed catalog storage data ownership (`DAILY/CHEST/PURCHASE/GOLD/EVENT`)

개별 클래스 구현 설명은 `10/11/12/13/14/15` 문서에서 다루며,
동작 순서/의미 충돌 시 이 문서가 우선한다.

---

## A) Terms

- `catalog instance`: `ShopCatalogBase` 파생 인스턴스 (`DAILY/CHEST/PURCHASE/GOLD/EVENT`)
- `catalog initialize`: `ShopCatalogBase.Initialize()` 1회 보장 초기화
- `catalog refresh`: `ShopCatalogBase.RefreshProducts()` 재실행
- `auto refresh`: 다음 refresh 시각(`autoRefreshUtcMs`)에 도달했을 때 카탈로그를 갱신하는 것
- `ads refill`: `adsRefreshUtcMs` 만료 시 ADS/FREE 제한 상품 `remainCount` 리필
- `daily manual refresh`: `SHOP_DAILY`가 광고 시청 성공으로 동적 5개 상품을 다시 뽑는 것
- `force catalog refresh`: `ShopCatalogBase.ResetAds()` 경로에서 강제 갱신

---

## B) Catalog Initialize Operation (정본)

`ShopManager.Initialize()` 호출 시:

1. `ensureCatalogInitialized()`를 먼저 수행한다.
2. 카탈로그 목록은 `ShopCatalogFactory.CreateRuntimeCatalogs(storage)`가 `TB_SHOP_CATALOG.GetAll()`을 읽어 생성한다. (하드코딩 금지)
3. `ShopManager`는 catalog를 먼저 registry에 등록한 뒤, 두 번째 pass에서 각 catalog `Initialize()`를 호출한다.
4. 각 카탈로그는 `ShopCatalogBase.Initialize()`를 호출해 storage/default 기반 1회 product 구성을 완료한다.
5. 초기화만으로 refresh를 강제하지 않는다.
6. 초기화 이후 refresh 조건 탐색/실행은 `tryRefreshAllCatalogs(...)`가 담당한다.
7. `TB_SHOP_CATALOG`가 비어 있거나 아직 로드되지 않은 경우, `_catalogInitialized`를 확정하지 않고 다음 호출에서 재시도한다.

규칙:
- `ShopManager.onInitAwake()`는 catalog를 초기화하지 않는다.
- `ShopManager.Initialize()`가 manager catalog 초기화의 유일한 진입점이다.
- initialize는 인스턴스 생성과 1회 setup의 책임만 가진다.
- runtime 조건(시간 만료/ads refill/강제 refresh)은 initialize에서 직접 판단하지 않는다.
- `ShopCatalogBase.Initialize()`는 `onInitialize()` 후 `RefreshProducts()`를 호출한다.
- 실제 product 생성 책임은 `ShopCatalogBase.onRefresh()`가 가진다.
- `ShopCatalogBase.onRefresh()` 기본 경로는 `CHEST/PURCHASE/GOLD`의 테이블 전체 row를 product로 생성하고, 같은 catalog bucket의 storage remain 상태를 즉시 적용한다.
- `ShopCatalogDaily.onRefresh()`는 valid storage가 있으면 storage 기준으로 5개 동적 상품을 복원하고, invalid/empty storage면 5개를 새로 선택 생성한다.
- `ShopCatalogEvent.onRefresh()`는 `SHOP_EVENT.start_time/end_time`을 서버 UTC 기준으로 평가해 현재 판매 중인 row만 product로 생성한다.
- `ShopCatalogBase`는 `Storage`를 소유하며, catalog runtime state helper는 catalog 계층에 둔다.
- `ShopCatalogBase`는 generic `StorageData`를 소유하며, 각 subclass는 자기 typed storage data를 해석한다.
- `ShopCatalogDaily`는 daily manual refresh 정책/state machine을 직접 가진다.
- `ShopManager`는 catalog-specific 정책을 직접 계산하지 않고, global refresh/index/save만 담당한다.
- daily storage의 만료 여부는 `onRefresh()`가 아니라 refresh 시간 판정에서 결정한다.
- 카탈로그 인스턴스 생성은 `ShopCatalogFactory`(14)에서, row -> `ShopProductBase` 변환은 `ShopProductFactory`(15)에서 처리한다.

---

## C) Catalog Refresh Condition Operation (정본)

카탈로그별 조건 탐색은 반드시
`evaluateCatalogRefreshState(catalog, requireServerTime, forceCatalogRefresh)` 1곳에서 수행한다.

산출 상태:
- `ShouldRefreshCatalogProducts`
- `ShouldRefillAdsFreeProducts`
- `ShouldInitializeAutoRefreshUtcMs`
- `ShouldClearAutoRefreshUtcMs`
- `ShouldClearAdsRefreshUtcMs`
- `RemainAutoRefreshTimeMs`
- `RemainAdsRefreshTimeMs`

판정 규칙:
- 일반 카탈로그는 `auto_refresh_days <= 0`이면 auto refresh 미사용 (`autoRefreshUtcMs` 제거 대상).
- `EVENT`는 `auto_refresh_days`를 사용하지 않고, 가장 가까운 미래 `start_time/end_time` 경계 시각을 다음 refresh 시각으로 사용한다.
- `requireServerTime=true`인데 서버 시간이 필요한 조건에서 시간이 없으면 실패.
- `forceCatalogRefresh=true`면 카탈로그 refresh를 강제한다.
- ADS/FREE 제한 상품이 없으면 `adsRefreshUtcMs`는 제거 대상이다.
- ADS/FREE 제한 상품이 있고 `adsRefreshUtcMs`가 없거나 만료면 refill 대상이다.

---

## D) Catalog Refresh Execute Operation (정본)

카탈로그별 refresh 실행은 반드시
`tryRefreshCatalog(catalog_type, requireServerTime, forceCatalogRefresh)` 1경로를 사용한다.

실행 순서:

1. `evaluateCatalogRefreshState(...)` 결과를 받는다.
2. `ShouldClearAutoRefreshUtcMs`면 clear한다.
3. `ShouldRefreshCatalogProducts`면:
   - `catalog.ClearRuntimeStateForRefresh(...)` 수행
   - `catalog.RefreshProducts()` 수행
   - 일반 auto refresh catalog는 다음 `autoRefreshUtcMs`를 기록
   - `EVENT`는 다음 `start_time/end_time` 경계 시각을 `autoRefreshUtcMs`로 기록
4. `ShouldRefreshCatalogProducts`가 false이고 `ShouldInitializeAutoRefreshUtcMs`면 초기 next refresh를 기록한다.
5. `ShouldClearAdsRefreshUtcMs`면 clear한다.
6. `ShouldRefillAdsFreeProducts`면 ADS/FREE 제한 상품만 `remainCount=max_count`로 리필하고 `adsRefreshUtcMs = serverNow + 1day`를 기록한다.
7. `DidRefreshCatalogProducts` / `DidMutateStorage` 결과를 반환한다.

규칙:
- `DAILY` refresh 시에는 `dailyCatalogProducts`를 비우고 5개 동적 상품을 재생성한다.
- ADS/FREE 리필 조건이 false이면 ADS/FREE remain 상태는 유지한다.
- `unlock_msg_id`가 `IsNullOrWhiteSpace`면 unlock 조건이 없는 카탈로그이며 `IsLocked=false`로 처리한다.

---

## E) Public API Contracts (정본)

`Initialize()`
- 초기화 + 조건부 refresh 전체 실행.
- 성공 시 catalog/product index 정합성 보장.

`RefreshProducts(requireServerTime=true)`
- 초기화 이후에만 조건부 refresh 전체 실행.
- catalog/product 초기화를 수행하지 않는다.

`GetCatalog<T>()`
- `ShopManager`는 typed catalog access를 위해 `GetCatalog<T>() where T : ShopCatalogBase`를 제공한다.
- `GetCatalog<T>()`는 typed catalog instance 획득용이다.
- catalog-specific operation은 획득한 catalog instance의 public method를 사용한다.

`ShopCatalogBase.ResetAds()`
- catalog instance public API다.
- 내부적으로 해당 catalogType에 대해 `forceCatalogRefresh=true` 경로를 호출한다.
- catalog는 공통 강제 refresh를 요청하고, ShopManager는 global post-commit(index/save)만 수행한다.

`ShopCatalogDaily.RefreshByAdsAsync()`
- `SHOP_DAILY` 전용 수동 refresh instance API다.
- `Initialize()` 이후에만 호출할 수 있다.
- `DefaultAdsAdvertiseId` 광고 시청 성공 시에만 성공한다.
- rolling 24시간 기준 최대 5회를 사용한다.
- 사용량 상태는 `manualRefreshUtcMs`(다음 만료 시각), `manualRefreshRemainCount`(남은 횟수)로 저장한다.
- 초기 상태와 만료 후 reset 상태의 `manualRefreshRemainCount`는 `5`다.
- 만료 후 첫 성공 시 `manualRefreshUtcMs = serverNow + 1day`, `manualRefreshRemainCount = 4`가 된다.
- 만료 전 재사용 시 `manualRefreshRemainCount--` 한다.
- 남은 횟수가 0이면 `SHOP_DAILY_MANUAL_REFRESH_COUNT_EXHAUSTED`로 실패한다.
- server time 검증과 manual refresh 상태 평가는 광고 시청 전에 완료해야 한다.
- manual refresh는 global refresh를 호출하지 않고 DAILY catalog만 1회 refresh한다.
- 광고 가용성 검증은 `AdsManager.ShowAsync(...)` 단일 경로를 사용한다.
- 성공 시 `manualRefreshUtcMs/manualRefreshRemainCount`뿐 아니라 `autoRefreshUtcMs`도 다음 주기로 갱신한다.
- 카탈로그별 남은 시간 조회는 별도 getter가 아니라 catalog runtime 프로퍼티를 직접 사용한다. 공통 값은 `RemainAutoRefreshTimeMs`, DAILY 전용 값은 `RemainAdsRefreshTimeMs`, `ManualRefreshRemainTimeMs`, `ManualRefreshRemainCount`다.

---

## F) SaveData Integration (정본)

- SaveData load는 `ShopStorage`에 저장 상태만 로드한다.
- `ShopStorage`는 generic dictionary가 아니라 catalog별 typed storage data field를 사용한다.
- 이후 `LoginManager -> ShopManager.Initialize()`가 storage 기반 runtime catalog/product 구성을 수행한다.
- `DAILY`를 제외한 카탈로그는 각 catalog의 `onRefresh()`가 table product 생성 + storage remain 적용을 직접 수행한다.
- `DAILY`는 `onRefresh()`에서 storage 기준 product 구성을 직접 복원할 수 있다.
- `DAILY` manual refresh 상태는 `ShopCatalogDaily.SyncRuntimeState(...)`가 storage 만료 여부를 정리하고 catalog runtime 프로퍼티에 동기화한다.
- `EVENT`는 별도 동적 payload를 저장하지 않고, `SHOP_EVENT` + 서버 시간 기준으로 매번 활성 상품을 재구성한다.
- 일반 shop runtime mutation은 로컬 save queue를 통해 저장한다. DAILY manual refresh는 `ShopCatalogDaily.RefreshByAdsAsync()`가 로컬 저장을 직접 수행한다.
- `ShopManager.synchronizeProductIndexFromCatalogs()`는 product index rebuild만 담당하며 storage restore를 수행하지 않는다.

---

## G) 3-path Mirroring (정본)

아래 파일은 동일 구현을 유지해야 한다.

- UPM (정본)
  - `framework-cs/upm/com.devian.foundation/Samples~/MobilePackage/Runtime/Shop/ShopManager.cs`
  - `framework-cs/upm/com.devian.foundation/Samples~/MobilePackage/Runtime/Shop/Catalog/` (8개 파일)
  - `framework-cs/upm/com.devian.foundation/Samples~/MobilePackage/Runtime/Shop/Product/` (8개 파일)
  - `framework-cs/upm/com.devian.foundation/Samples~/MobilePackage/Runtime/Shop/ShopProductFactory.cs`
  - `framework-cs/upm/com.devian.foundation/Samples~/MobilePackage/Runtime/Shop/ShopStorage.cs`
  - `framework-cs/upm/com.devian.foundation/Samples~/MobilePackage/Runtime/SaveData/JsonCodec/SaveDataJsonCodecShop.cs`
- Packages (sync)
  - `framework-cs/apps/UnityExample/Packages/com.devian.foundation/Samples~/MobilePackage/Runtime/Shop/ShopManager.cs`
  - `framework-cs/apps/UnityExample/Packages/com.devian.foundation/Samples~/MobilePackage/Runtime/Shop/Catalog/` (8개 파일)
  - `framework-cs/apps/UnityExample/Packages/com.devian.foundation/Samples~/MobilePackage/Runtime/Shop/Product/` (8개 파일)
  - `framework-cs/apps/UnityExample/Packages/com.devian.foundation/Samples~/MobilePackage/Runtime/Shop/ShopProductFactory.cs`
  - `framework-cs/apps/UnityExample/Packages/com.devian.foundation/Samples~/MobilePackage/Runtime/Shop/ShopStorage.cs`
  - `framework-cs/apps/UnityExample/Packages/com.devian.foundation/Samples~/MobilePackage/Runtime/SaveData/JsonCodec/SaveDataJsonCodecShop.cs`
- Assets/Samples (import)
  - `framework-cs/apps/UnityExample/Assets/Samples/Devian Samples/{version}/MobilePackage/Runtime/Shop/ShopManager.cs`
  - `framework-cs/apps/UnityExample/Assets/Samples/Devian Samples/{version}/MobilePackage/Runtime/Shop/Catalog/` (8개 파일)
  - `framework-cs/apps/UnityExample/Assets/Samples/Devian Samples/{version}/MobilePackage/Runtime/Shop/Product/` (8개 파일)
  - `framework-cs/apps/UnityExample/Assets/Samples/Devian Samples/{version}/MobilePackage/Runtime/Shop/ShopProductFactory.cs`
  - `framework-cs/apps/UnityExample/Assets/Samples/Devian Samples/{version}/MobilePackage/Runtime/Shop/ShopStorage.cs`
  - `framework-cs/apps/UnityExample/Assets/Samples/Devian Samples/{version}/MobilePackage/Runtime/SaveData/JsonCodec/SaveDataJsonCodecShop.cs`

---

## Related

- [00-overview](../00-overview/SKILL.md)
- [10-shop-manager](../10-shop-manager/SKILL.md)
- [12-shop-storage](../12-shop-storage/SKILL.md)
- [13-shop-catalog](../13-shop-catalog/SKILL.md)
