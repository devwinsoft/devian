namespace Devian
{
    /// <summary>
    /// initSession callable 응답.
    /// getMissionClock + getEntitlements + getPurchaseAdjustments(첫 페이지) 통합 결과.
    /// </summary>
    public readonly struct SessionInitSnapshot
    {
        public SessionInitSnapshot(
            MissionClockSnapshot missionClock,
            EntitlementsSnapshot entitlements,
            RefundPageResult purchaseAdjustments)
        {
            MissionClock = missionClock;
            Entitlements = entitlements;
            PurchaseAdjustments = purchaseAdjustments;
        }

        public MissionClockSnapshot MissionClock { get; }
        public EntitlementsSnapshot Entitlements { get; }
        public RefundPageResult PurchaseAdjustments { get; }
    }
}
