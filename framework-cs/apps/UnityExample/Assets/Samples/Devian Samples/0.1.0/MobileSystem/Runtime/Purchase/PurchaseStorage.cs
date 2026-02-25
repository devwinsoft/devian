using System;
using System.Collections.Generic;

namespace Devian
{
    /// <summary>
    /// Minimal client-side purchase progress snapshot.
    /// Stores only the current in-progress purchase state required for local recovery.
    /// Does not store purchase failure history, raw receipts, or tokens.
    /// Stores current/refundSupportLogs only. Refund deduplication is server-side (clientRefundApplied).
    /// </summary>
    public sealed class PurchaseStorage
    {
        const int MaxRefundSupportLogs = 32;
        const int RefundSupportLogRetentionDays = 30;
        const long MsPerDay = 24L * 60L * 60L * 1000L;

        readonly CurrentPurchaseState _current = new();
        readonly List<RefundSupportLogEntry> _refundSupportLogs = new();
        public CurrentPurchaseState Current => _current;
        public IReadOnlyList<RefundSupportLogEntry> RefundSupportLogs => _refundSupportLogs;

        public void BeginPurchase(string internalProductId, string kind, string storeKey)
        {
            _current.IsPurchaseInProgress = true;
            _current.InternalProductId = internalProductId ?? string.Empty;
            _current.Kind = kind ?? string.Empty;
            _current.StoreKey = storeKey ?? string.Empty;
            _current.StartedAtUtcMs = nowUtcMs();
            _current.IsStorePending = false;
            _current.StorePendingAtUtcMs = 0;

            _current.PurchaseId = string.Empty;
            _current.VerifyStatus = string.Empty;
            _current.StoreConfirmedLocal = false;
            _current.ClientGrantApplied = false;
            _current.ClientGrantReported = false;
            _current.VerifyRetryCount = 0;
        }

        public void MarkStorePending()
        {
            if (!_current.IsPurchaseInProgress)
                return;

            _current.IsStorePending = true;
            if (_current.StorePendingAtUtcMs <= 0)
                _current.StorePendingAtUtcMs = nowUtcMs();
        }

        public void MarkVerifyAccepted(string purchaseId, string verifyStatus)
        {
            if (!_current.IsPurchaseInProgress)
                return;

            _current.PurchaseId = purchaseId ?? string.Empty;
            _current.VerifyStatus = verifyStatus ?? string.Empty;
        }

        public void MarkClientGrantApplied()
        {
            if (!_current.IsPurchaseInProgress)
                return;

            _current.ClientGrantApplied = true;
        }

        public void MarkStoreConfirmedLocal()
        {
            if (!_current.IsPurchaseInProgress)
                return;

            _current.StoreConfirmedLocal = true;
        }

        public void MarkClientGrantReported()
        {
            if (!_current.IsPurchaseInProgress)
                return;

            _current.ClientGrantReported = true;
        }

        public void IncrementVerifyRetryCount()
        {
            if (!_current.IsPurchaseInProgress)
                return;

            _current.VerifyRetryCount++;
        }

        public void UpsertRefundSupportLog(
            string purchaseId,
            string internalProductId,
            string kind,
            string storeKey,
            string verifyStatus,
            string clientGrantStatus,
            string storeConfirmStatus)
        {
            if (string.IsNullOrEmpty(purchaseId))
                return;

            var now = nowUtcMs();
            pruneExpiredRefundSupportLogs(now);

            var existingIndex = -1;
            for (var i = 0; i < _refundSupportLogs.Count; i++)
            {
                if (string.Equals(_refundSupportLogs[i].PurchaseId, purchaseId, StringComparison.Ordinal))
                {
                    existingIndex = i;
                    break;
                }
            }

            if (existingIndex >= 0)
            {
                var existing = _refundSupportLogs[existingIndex];
                existing.Update(
                    internalProductId,
                    kind,
                    storeKey,
                    verifyStatus,
                    clientGrantStatus,
                    storeConfirmStatus,
                    now);

                // Move updated item to tail to keep chronological order (oldest -> newest).
                _refundSupportLogs.RemoveAt(existingIndex);
                _refundSupportLogs.Add(existing);
            }
            else
            {
                _refundSupportLogs.Add(new RefundSupportLogEntry(
                    purchaseId,
                    internalProductId,
                    kind,
                    storeKey,
                    verifyStatus,
                    clientGrantStatus,
                    storeConfirmStatus,
                    now));
            }

            trimRefundSupportLogs();
        }

        public IReadOnlyList<RefundSupportLogEntry> GetRefundSupportLogs()
        {
            return _refundSupportLogs;
        }

        public bool TryGetRefundSupportLog(string purchaseId, out RefundSupportLogEntry entry)
        {
            entry = null;
            if (string.IsNullOrEmpty(purchaseId))
                return false;

            for (var i = _refundSupportLogs.Count - 1; i >= 0; i--)
            {
                var candidate = _refundSupportLogs[i];
                if (!string.Equals(candidate.PurchaseId, purchaseId, StringComparison.Ordinal))
                    continue;

                entry = candidate;
                return true;
            }

            return false;
        }

        public bool RemoveRefundSupportLog(string purchaseId)
        {
            if (string.IsNullOrEmpty(purchaseId))
                return false;

            for (var i = 0; i < _refundSupportLogs.Count; i++)
            {
                if (!string.Equals(_refundSupportLogs[i].PurchaseId, purchaseId, StringComparison.Ordinal))
                    continue;

                _refundSupportLogs.RemoveAt(i);
                return true;
            }

            return false;
        }

        public void ClearRefundSupportLogs()
        {
            _refundSupportLogs.Clear();
        }

        public void PruneRefundSupportLogs()
        {
            var now = nowUtcMs();
            pruneExpiredRefundSupportLogs(now);
            trimRefundSupportLogs();
        }

        public void ClearCurrent()
        {
            _current.IsPurchaseInProgress = false;
            _current.InternalProductId = string.Empty;
            _current.Kind = string.Empty;
            _current.StoreKey = string.Empty;
            _current.StartedAtUtcMs = 0;
            _current.IsStorePending = false;
            _current.StorePendingAtUtcMs = 0;
            _current.PurchaseId = string.Empty;
            _current.VerifyStatus = string.Empty;
            _current.StoreConfirmedLocal = false;
            _current.ClientGrantApplied = false;
            _current.ClientGrantReported = false;
            _current.VerifyRetryCount = 0;
        }

        public void ClearAll()
        {
            ClearCurrent();
            ClearRefundSupportLogs();
        }

        public void RestoreCurrent(
            bool isPurchaseInProgress,
            string internalProductId,
            string kind,
            string storeKey,
            long startedAtUtcMs,
            bool storePending,
            long storePendingAtUtcMs,
            string purchaseId,
            string verifyStatus,
            bool storeConfirmedLocal,
            bool clientGrantApplied,
            bool clientGrantReported,
            int verifyRetryCount)
        {
            _current.IsPurchaseInProgress = isPurchaseInProgress;
            _current.InternalProductId = internalProductId ?? string.Empty;
            _current.Kind = kind ?? string.Empty;
            _current.StoreKey = storeKey ?? string.Empty;
            _current.StartedAtUtcMs = startedAtUtcMs;
            _current.IsStorePending = storePending;
            _current.StorePendingAtUtcMs = storePendingAtUtcMs;
            _current.PurchaseId = purchaseId ?? string.Empty;
            _current.VerifyStatus = verifyStatus ?? string.Empty;
            _current.StoreConfirmedLocal = storeConfirmedLocal;
            _current.ClientGrantApplied = clientGrantApplied;
            _current.ClientGrantReported = clientGrantReported;
            _current.VerifyRetryCount = verifyRetryCount;
        }

        public void RestoreRefundSupportLogs(IEnumerable<RefundSupportLogRestoreItem> items)
        {
            _refundSupportLogs.Clear();
            if (items == null)
                return;

            foreach (var item in items)
            {
                if (string.IsNullOrEmpty(item.PurchaseId))
                    continue;

                _refundSupportLogs.Add(new RefundSupportLogEntry(
                    item.PurchaseId,
                    item.InternalProductId,
                    item.Kind,
                    item.StoreKey,
                    item.VerifyStatus,
                    item.ClientGrantStatus,
                    item.StoreConfirmStatus,
                    item.FirstSeenAtUtcMs,
                    item.LastUpdatedAtUtcMs));
            }

            PruneRefundSupportLogs();
        }

        void trimRefundSupportLogs()
        {
            if (_refundSupportLogs.Count <= MaxRefundSupportLogs)
                return;

            _refundSupportLogs.RemoveRange(0, _refundSupportLogs.Count - MaxRefundSupportLogs);
        }

        void pruneExpiredRefundSupportLogs(long nowUtcMs)
        {
            var cutoffUtcMs = nowUtcMs - (RefundSupportLogRetentionDays * MsPerDay);
            _refundSupportLogs.RemoveAll(log =>
                log != null &&
                log.LastUpdatedAtUtcMs > 0 &&
                log.LastUpdatedAtUtcMs < cutoffUtcMs);
        }

        static long nowUtcMs() => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        public sealed class CurrentPurchaseState
        {
            // Mutated by PurchaseStorage. Kept non-public to discourage direct writes from callers.
            public bool IsPurchaseInProgress { get; internal set; }
            public string InternalProductId { get; internal set; } = string.Empty;
            public string Kind { get; internal set; } = string.Empty;
            public string StoreKey { get; internal set; } = string.Empty;
            public long StartedAtUtcMs { get; internal set; }
            public bool IsStorePending { get; internal set; }
            public long StorePendingAtUtcMs { get; internal set; }
            public string PurchaseId { get; internal set; } = string.Empty;
            public string VerifyStatus { get; internal set; } = string.Empty;
            public bool StoreConfirmedLocal { get; internal set; }
            public bool ClientGrantApplied { get; internal set; }
            public bool ClientGrantReported { get; internal set; }
            public int VerifyRetryCount { get; internal set; }
        }

        public sealed class RefundSupportLogEntry
        {
            internal RefundSupportLogEntry(
                string purchaseId,
                string internalProductId,
                string kind,
                string storeKey,
                string verifyStatus,
                string clientGrantStatus,
                string storeConfirmStatus,
                long nowUtcMs)
                : this(
                    purchaseId,
                    internalProductId,
                    kind,
                    storeKey,
                    verifyStatus,
                    clientGrantStatus,
                    storeConfirmStatus,
                    nowUtcMs,
                    nowUtcMs)
            {
            }

            internal RefundSupportLogEntry(
                string purchaseId,
                string internalProductId,
                string kind,
                string storeKey,
                string verifyStatus,
                string clientGrantStatus,
                string storeConfirmStatus,
                long firstSeenAtUtcMs,
                long lastUpdatedAtUtcMs)
            {
                PurchaseId = purchaseId ?? string.Empty;
                InternalProductId = internalProductId ?? string.Empty;
                Kind = kind ?? string.Empty;
                StoreKey = storeKey ?? string.Empty;
                VerifyStatus = verifyStatus ?? string.Empty;
                ClientGrantStatus = clientGrantStatus ?? string.Empty;
                StoreConfirmStatus = storeConfirmStatus ?? string.Empty;
                FirstSeenAtUtcMs = firstSeenAtUtcMs;
                LastUpdatedAtUtcMs = lastUpdatedAtUtcMs;
            }

            public string PurchaseId { get; private set; }
            public string InternalProductId { get; private set; }
            public string Kind { get; private set; }
            public string StoreKey { get; private set; }
            public string VerifyStatus { get; private set; }
            public string ClientGrantStatus { get; private set; }
            public string StoreConfirmStatus { get; private set; }
            public long FirstSeenAtUtcMs { get; private set; }
            public long LastUpdatedAtUtcMs { get; private set; }

            internal void Update(
                string internalProductId,
                string kind,
                string storeKey,
                string verifyStatus,
                string clientGrantStatus,
                string storeConfirmStatus,
                long updatedAtUtcMs)
            {
                if (!string.IsNullOrEmpty(internalProductId))
                    InternalProductId = internalProductId;
                if (!string.IsNullOrEmpty(kind))
                    Kind = kind;
                if (!string.IsNullOrEmpty(storeKey))
                    StoreKey = storeKey;
                if (!string.IsNullOrEmpty(verifyStatus))
                    VerifyStatus = verifyStatus;
                if (!string.IsNullOrEmpty(clientGrantStatus))
                    ClientGrantStatus = clientGrantStatus;
                if (!string.IsNullOrEmpty(storeConfirmStatus))
                    StoreConfirmStatus = storeConfirmStatus;

                LastUpdatedAtUtcMs = updatedAtUtcMs;
            }
        }

        public readonly struct RefundSupportLogRestoreItem
        {
            public RefundSupportLogRestoreItem(
                string purchaseId,
                string internalProductId,
                string kind,
                string storeKey,
                string verifyStatus,
                string clientGrantStatus,
                string storeConfirmStatus,
                long firstSeenAtUtcMs,
                long lastUpdatedAtUtcMs)
            {
                PurchaseId = purchaseId ?? string.Empty;
                InternalProductId = internalProductId ?? string.Empty;
                Kind = kind ?? string.Empty;
                StoreKey = storeKey ?? string.Empty;
                VerifyStatus = verifyStatus ?? string.Empty;
                ClientGrantStatus = clientGrantStatus ?? string.Empty;
                StoreConfirmStatus = storeConfirmStatus ?? string.Empty;
                FirstSeenAtUtcMs = firstSeenAtUtcMs;
                LastUpdatedAtUtcMs = lastUpdatedAtUtcMs;
            }

            public string PurchaseId { get; }
            public string InternalProductId { get; }
            public string Kind { get; }
            public string StoreKey { get; }
            public string VerifyStatus { get; }
            public string ClientGrantStatus { get; }
            public string StoreConfirmStatus { get; }
            public long FirstSeenAtUtcMs { get; }
            public long LastUpdatedAtUtcMs { get; }
        }
    }
}
