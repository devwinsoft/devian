using System;

namespace Devian
{
    /// <summary>
    /// Minimal client-side purchase state snapshot.
    /// Stores only the current in-progress purchase and the latest result summary.
    /// It is not a purchase ledger and must not store raw receipts or auth tokens.
    /// </summary>
    public sealed class PurchaseStorage
    {
        // Current (in-progress) purchase snapshot
        public bool IsPurchaseInProgress { get; private set; }
        public string CurrentInternalProductId { get; private set; } = string.Empty;
        public string CurrentKind { get; private set; } = string.Empty;
        public string CurrentStoreKey { get; private set; } = string.Empty;
        public long CurrentStartedAtUtcMs { get; private set; }
        public bool CurrentStorePending { get; private set; }
        public long CurrentStorePendingAtUtcMs { get; private set; }

        // Last purchase result summary (single record only)
        public string LastInternalProductId { get; private set; } = string.Empty;
        public string LastKind { get; private set; } = string.Empty;
        public string LastStoreKey { get; private set; } = string.Empty;
        public string LastResultStatus { get; private set; } = string.Empty;
        public string LastErrorCode { get; private set; } = string.Empty;
        public string LastErrorMessage { get; private set; } = string.Empty;
        public long LastUpdatedAtUtcMs { get; private set; }

        public void BeginPurchase(string internalProductId, string kind, string storeKey)
        {
            IsPurchaseInProgress = true;
            CurrentInternalProductId = internalProductId ?? string.Empty;
            CurrentKind = kind ?? string.Empty;
            CurrentStoreKey = storeKey ?? string.Empty;
            CurrentStartedAtUtcMs = nowUtcMs();
            CurrentStorePending = false;
            CurrentStorePendingAtUtcMs = 0;
        }

        public void MarkStorePending()
        {
            if (!IsPurchaseInProgress)
                return;

            CurrentStorePending = true;
            if (CurrentStorePendingAtUtcMs <= 0)
                CurrentStorePendingAtUtcMs = nowUtcMs();
        }

        public void MarkStoreFailed(string errorCode, string errorMessage)
        {
            updateLast("STORE_FAILED", errorCode, errorMessage);
        }

        public void MarkVerifySucceeded(string resultStatus)
        {
            updateLast(string.IsNullOrEmpty(resultStatus) ? "GRANTED" : resultStatus, string.Empty, string.Empty);
        }

        public void MarkVerifyFailed(string errorCode, string errorMessage, string resultStatus = null)
        {
            updateLast(string.IsNullOrEmpty(resultStatus) ? "VERIFY_FAILED" : resultStatus, errorCode, errorMessage);
        }

        public void ClearCurrent()
        {
            IsPurchaseInProgress = false;
            CurrentInternalProductId = string.Empty;
            CurrentKind = string.Empty;
            CurrentStoreKey = string.Empty;
            CurrentStartedAtUtcMs = 0;
            CurrentStorePending = false;
            CurrentStorePendingAtUtcMs = 0;
        }

        public void ClearAll()
        {
            ClearCurrent();

            LastInternalProductId = string.Empty;
            LastKind = string.Empty;
            LastStoreKey = string.Empty;
            LastResultStatus = string.Empty;
            LastErrorCode = string.Empty;
            LastErrorMessage = string.Empty;
            LastUpdatedAtUtcMs = 0;
        }

        // Restore snapshot from GameStorageManager save payload.
        public void RestoreCurrent(
            bool isPurchaseInProgress,
            string internalProductId,
            string kind,
            string storeKey,
            long startedAtUtcMs,
            bool storePending,
            long storePendingAtUtcMs)
        {
            IsPurchaseInProgress = isPurchaseInProgress;
            CurrentInternalProductId = internalProductId ?? string.Empty;
            CurrentKind = kind ?? string.Empty;
            CurrentStoreKey = storeKey ?? string.Empty;
            CurrentStartedAtUtcMs = startedAtUtcMs;
            CurrentStorePending = storePending;
            CurrentStorePendingAtUtcMs = storePendingAtUtcMs;
        }

        public void RestoreLast(
            string internalProductId,
            string kind,
            string storeKey,
            string resultStatus,
            string errorCode,
            string errorMessage,
            long updatedAtUtcMs)
        {
            LastInternalProductId = internalProductId ?? string.Empty;
            LastKind = kind ?? string.Empty;
            LastStoreKey = storeKey ?? string.Empty;
            LastResultStatus = resultStatus ?? string.Empty;
            LastErrorCode = errorCode ?? string.Empty;
            LastErrorMessage = errorMessage ?? string.Empty;
            LastUpdatedAtUtcMs = updatedAtUtcMs;
        }

        void updateLast(string resultStatus, string errorCode, string errorMessage)
        {
            LastInternalProductId = CurrentInternalProductId;
            LastKind = CurrentKind;
            LastStoreKey = CurrentStoreKey;
            LastResultStatus = resultStatus ?? string.Empty;
            LastErrorCode = errorCode ?? string.Empty;
            LastErrorMessage = errorMessage ?? string.Empty;
            LastUpdatedAtUtcMs = nowUtcMs();
        }

        static long nowUtcMs()
        {
            return DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        }
    }
}
