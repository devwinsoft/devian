namespace Devian
{
    /// <summary>
    /// initSession callable 응답.
    /// getRemoteConfig + getEntitlements + getPurchaseAdjustments(첫 페이지) 통합 결과.
    /// </summary>
    public readonly struct SessionInitSnapshot
    {
        public SessionInitSnapshot(
            RemoteConfigSnapshot remoteConfig,
            EntitlementsSnapshot entitlements,
            RefundPageResult purchaseAdjustments)
        {
            RemoteConfig = remoteConfig;
            Entitlements = entitlements;
            PurchaseAdjustments = purchaseAdjustments;
        }

        public RemoteConfigSnapshot RemoteConfig { get; }
        public EntitlementsSnapshot Entitlements { get; }
        public RefundPageResult PurchaseAdjustments { get; }
    }
}
