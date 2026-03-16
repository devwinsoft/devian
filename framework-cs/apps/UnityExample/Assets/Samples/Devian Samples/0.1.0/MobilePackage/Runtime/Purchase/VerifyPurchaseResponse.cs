namespace Devian
{
    public readonly struct VerifyPurchaseResponse
    {
        public VerifyPurchaseResponse(
            string resultStatus,
            string rejectReason,
            string purchaseId,
            string verifyStatus,
            string clientGrantStatus,
            string storeConfirmStatus,
            EntitlementsSnapshot? snapshot)
        {
            ResultStatus = resultStatus;
            RejectReason = rejectReason;
            PurchaseId = purchaseId;
            VerifyStatus = verifyStatus;
            ClientGrantStatus = clientGrantStatus;
            StoreConfirmStatus = storeConfirmStatus;
            Snapshot = snapshot;
        }

        public string ResultStatus { get; }
        public string RejectReason { get; }
        public string PurchaseId { get; }
        public string VerifyStatus { get; }
        public string ClientGrantStatus { get; }
        public string StoreConfirmStatus { get; }
        public EntitlementsSnapshot? Snapshot { get; }
    }
}
