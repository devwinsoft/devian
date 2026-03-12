namespace Devian
{
    /// <summary>
    /// initSession callable 응답.
    /// getEntitlements + getPurchaseAdjustments(첫 페이지) 통합 결과.
    /// </summary>
    public readonly struct SessionInitSnapshot
    {
        public SessionInitSnapshot(
            EntitlementsSnapshot entitlements,
            RefundPageResult purchaseAdjustments)
        {
            Entitlements = entitlements;
            PurchaseAdjustments = purchaseAdjustments;
        }

        public EntitlementsSnapshot Entitlements { get; }
        public RefundPageResult PurchaseAdjustments { get; }
    }
}
