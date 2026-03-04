namespace Devian
{
    /// <summary>
    /// Firebase getPurchaseAdjustments 응답의 raw 아이템.
    /// domain 변환(rewardGroupId, rewards, parsed kind)은 포함하지 않는다.
    /// </summary>
    public readonly struct PurchaseAdjustmentItem
    {
        public PurchaseAdjustmentItem(
            string purchaseId,
            string internalProductId,
            string resultStatus,
            string kindString,
            string reason,
            long updatedAtUtcMs)
        {
            PurchaseId = purchaseId ?? string.Empty;
            InternalProductId = internalProductId ?? string.Empty;
            ResultStatus = resultStatus ?? string.Empty;
            KindString = kindString ?? string.Empty;
            Reason = reason ?? string.Empty;
            UpdatedAtUtcMs = updatedAtUtcMs;
        }

        public string PurchaseId { get; }
        public string InternalProductId { get; }
        public string ResultStatus { get; }
        public string KindString { get; }
        public string Reason { get; }
        public long UpdatedAtUtcMs { get; }
    }
}
