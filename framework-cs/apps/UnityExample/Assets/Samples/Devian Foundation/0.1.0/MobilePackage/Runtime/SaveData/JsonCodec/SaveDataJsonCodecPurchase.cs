using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace Devian
{
    internal static class SaveDataJsonCodecPurchase
    {
        public static JObject Serialize(PurchaseStorage purchase)
        {
            purchase.PruneRefundSupportLogs();

            var currentState = purchase.Current;
            var current = new JObject
            {
                ["isPurchaseInProgress"] = currentState.IsPurchaseInProgress,
                ["internal_product_id"] = currentState.InternalProductId,
                ["kind"] = currentState.Kind,
                ["storeKey"] = currentState.StoreKey,
                ["startedAtUtcMs"] = currentState.StartedAtUtcMs,
                ["isStorePending"] = currentState.IsStorePending,
                ["storePendingAtUtcMs"] = currentState.StorePendingAtUtcMs,
                ["purchaseId"] = currentState.PurchaseId,
                ["verifyStatus"] = currentState.VerifyStatus,
                ["storeConfirmedLocal"] = currentState.StoreConfirmedLocal,
                ["clientGrantApplied"] = currentState.ClientGrantApplied,
                ["clientGrantReported"] = currentState.ClientGrantReported,
                ["verifyRetryCount"] = currentState.VerifyRetryCount,
            };

            var refundSupportLogs = new JArray();
            foreach (var log in purchase.RefundSupportLogs)
            {
                refundSupportLogs.Add(new JObject
                {
                    ["purchaseId"] = log.PurchaseId,
                    ["internal_product_id"] = log.InternalProductId,
                    ["kind"] = log.Kind,
                    ["storeKey"] = log.StoreKey,
                    ["verifyStatus"] = log.VerifyStatus,
                    ["clientGrantStatus"] = log.ClientGrantStatus,
                    ["storeConfirmStatus"] = log.StoreConfirmStatus,
                    ["firstSeenAtUtcMs"] = log.FirstSeenAtUtcMs,
                    ["lastUpdatedAtUtcMs"] = log.LastUpdatedAtUtcMs,
                });
            }

            return new JObject
            {
                ["current"] = current,
                ["refundSupportLogs"] = refundSupportLogs,
            };
        }

        public static void DeserializeInto(JObject purchaseObj, PurchaseStorage purchase)
        {
            purchase.ClearAll();

            if (purchaseObj["current"] is JObject currentObj)
            {
                purchase.RestoreCurrent(
                    currentObj.Value<bool?>("isPurchaseInProgress") ?? false,
                    currentObj.Value<string>("internal_product_id") ?? string.Empty,
                    currentObj.Value<string>("kind") ?? string.Empty,
                    currentObj.Value<string>("storeKey") ?? string.Empty,
                    currentObj.Value<long?>("startedAtUtcMs") ?? 0L,
                    currentObj.Value<bool?>("isStorePending") ?? false,
                    currentObj.Value<long?>("storePendingAtUtcMs") ?? 0L,
                    currentObj.Value<string>("purchaseId") ?? string.Empty,
                    currentObj.Value<string>("verifyStatus") ?? string.Empty,
                    currentObj.Value<bool?>("storeConfirmedLocal") ?? false,
                    currentObj.Value<bool?>("clientGrantApplied") ?? false,
                    currentObj.Value<bool?>("clientGrantReported")
                        ?? currentObj.Value<bool?>("serverAcked")
                        ?? false,
                    currentObj.Value<int?>("verifyRetryCount") ?? 0);
            }

            if (purchaseObj["refundSupportLogs"] is JArray refundLogsArr)
            {
                var restoreItems = new List<PurchaseStorage.RefundSupportLogRestoreItem>(refundLogsArr.Count);
                foreach (var token in refundLogsArr)
                {
                    if (!(token is JObject logObj))
                        continue;

                    restoreItems.Add(new PurchaseStorage.RefundSupportLogRestoreItem(
                        logObj.Value<string>("purchaseId") ?? string.Empty,
                        logObj.Value<string>("internal_product_id") ?? string.Empty,
                        logObj.Value<string>("kind") ?? string.Empty,
                        logObj.Value<string>("storeKey") ?? string.Empty,
                        logObj.Value<string>("verifyStatus") ?? string.Empty,
                        logObj.Value<string>("clientGrantStatus") ?? string.Empty,
                        logObj.Value<string>("storeConfirmStatus") ?? string.Empty,
                        logObj.Value<long?>("firstSeenAtUtcMs") ?? 0L,
                        logObj.Value<long?>("lastUpdatedAtUtcMs") ?? 0L));
                }

                purchase.RestoreRefundSupportLogs(restoreItems);
            }

            // 하위호환: 기존 저장 데이터에 "refundSync" 키가 있어도 무시.
            // Refund 중복 방지는 서버 clientRefundApplied 필드로 처리.
        }
    }
}
