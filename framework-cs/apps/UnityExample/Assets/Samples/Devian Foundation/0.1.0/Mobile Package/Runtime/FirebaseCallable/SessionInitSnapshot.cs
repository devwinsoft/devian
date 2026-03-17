namespace Devian
{
    /// <summary>
    /// initSession callable 응답.
    /// getPurchaseAdjustments(첫 페이지) 결과.
    /// </summary>
    public readonly struct SessionInitSnapshot
    {
        public SessionInitSnapshot(RefundPageResult purchaseAdjustments)
        {
            PurchaseAdjustments = purchaseAdjustments;
        }

        public RefundPageResult PurchaseAdjustments { get; }
    }
}
