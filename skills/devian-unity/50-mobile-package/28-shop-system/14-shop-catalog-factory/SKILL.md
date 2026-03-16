---
name: 14-shop-catalog-factory
description: "`ShopCatalogFactory`로 `SHOP_CATALOG` 테이블 기반 카탈로그 인스턴스 생성/dispatch 경로를 표준화할 때 사용한다."
---

# 14-shop-catalog-factory

Status: ACTIVE
AppliesTo: v10
Type: Design / Factory SSOT

## Purpose

`ShopCatalogFactory`는 `SHOP_CATALOG` 테이블 기반 카탈로그 인스턴스 생성 경로를 표준화한다.

- `catalogType` → 구현 클래스 dispatch를 한 곳에서 수행한다.
- runtime 생성 시 catalog-specific storage data 주입을 한 곳에서 수행한다.
- 카탈로그 초기화(`Initialize()`) 호출 시점을 경로별로 분리한다.

---

## Factory Shape

```csharp
internal static class ShopCatalogFactory
{
    public static IReadOnlyList<ShopCatalogBase> CreateRuntimeCatalogs(ShopStorage storage);
    public static ShopCatalogBase Create(SHOP_CATALOG_TYPE catalogType);
    public static ShopCatalogBase Create(SHOP_CATALOG_TYPE catalogType, IReadOnlyList<ShopProductBase> products);
    public static ShopCatalogBase Create(ShopCatalogBase sourceCatalog, IReadOnlyList<ShopProductBase> products);
    public static ShopCatalogBase Empty(SHOP_CATALOG_TYPE catalogType);
}
```

---

## Hard Rules

- 카탈로그 목록은 `TB_SHOP_CATALOG.GetAll()`로 생성한다. (하드코딩 금지)
- `CreateRuntimeCatalogs`는 runtime storage를 연결한 catalog 인스턴스만 생성하고 `Initialize()`를 호출하지 않는다.
- `CreateRuntimeCatalogs`는 `ShopStorage.GetCatalogData(catalogType)`로 얻은 typed storage data를 각 catalog 생성자에 전달한다.
- `DAILY/CHEST/PURCHASE/GOLD/EVENT`는 각각 `ShopCatalogDailyStorageData`, `ShopCatalogChestStorageData`, `ShopCatalogPurchaseStorageData`, `ShopCatalogGoldStorageData`, `ShopCatalogEventStorageData`를 사용한다.
- standalone `Create(...)` 는 반환 전 `Initialize()`를 호출해 product 인덱스를 확정한다.
- `catalogType` → 구현 클래스(`ShopCatalogDaily/Event/Chest/Purchase/Gold/Empty`) dispatch는 이 파일에서만 수행한다.
- Factory는 product 생성 책임을 가지지 않는다. product 생성은 `ShopProductFactory`(15)가 담당한다.
- `Create(ShopCatalogBase sourceCatalog, ...)` 는 source의 `CatalogConfig`와 `IsLocked`를 복제한다.

---

## Create Path Summary

| Method | Initialize | 용도 |
|--------|-----------|------|
| `CreateRuntimeCatalogs` | X | `ShopManager.ensureCatalogInitialized`에서 storage-backed bulk 생성 |
| `Create(catalogType)` | O | standalone catalog (테스트/UI 미리보기) |
| `Create(catalogType, products)` | O | prebuilt products로 standalone catalog |
| `Create(sourceCatalog, products)` | O | source config 복제 + 새 products |
| `Empty(catalogType)` | O | 빈 placeholder catalog |

---

## Implementation Location (3-path mirror)

- UPM (정본): `framework-cs/upm/com.devian.foundation/Samples~/MobilePackage/Runtime/Shop/Catalog/ShopCatalogFactory.cs`
- Packages (sync): `framework-cs/apps/UnityExample/Packages/com.devian.foundation/Samples~/MobilePackage/Runtime/Shop/Catalog/ShopCatalogFactory.cs`
- Assets/Samples (import): `framework-cs/apps/UnityExample/Assets/Samples/Devian Samples/{version}/MobilePackage/Runtime/Shop/Catalog/ShopCatalogFactory.cs`

---

## Related

- [03-ssot](../03-ssot/SKILL.md)
- [13-shop-catalog](../13-shop-catalog/SKILL.md)
- [15-shop-product-factory](../15-shop-product-factory/SKILL.md)
