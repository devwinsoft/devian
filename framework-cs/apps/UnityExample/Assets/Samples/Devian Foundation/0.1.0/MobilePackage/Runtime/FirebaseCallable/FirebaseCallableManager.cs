using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Devian.Domain.Common;
using Devian.Domain.Game;
using Firebase.Functions;
using UnityEngine;

namespace Devian
{
    /// <summary>
    /// Firebase Cloud Functions callable 통합 래퍼.
    /// 함수별 typed public API, 에러 매핑, 응답 파싱, region 관리, 값 추출 헬퍼를 통합한다.
    /// </summary>
    internal sealed class FirebaseCallableManager : CompoSingleton<FirebaseCallableManager>
    {
        const string Tag = "FirebaseCallableManager";

        string _functionsRegion = string.Empty;

        protected override void onInitAwake()
        {
            tryInitializeFunctionsRegionFromApplication();
        }

        void tryInitializeFunctionsRegionFromApplication()
        {
            if (!string.IsNullOrEmpty(_functionsRegion))
                return;

            var app = MobileApplication.Instance;
            if (app == null)
                return;

            _functionsRegion = string.IsNullOrWhiteSpace(app.FirebaseFunctionsRegion)
                ? string.Empty
                : app.FirebaseFunctionsRegion.Trim();
        }

        // ── Session Init (로그인 시 일괄 조회) ──────────────────────

        /// <summary>
        /// initSession callable — getPurchaseAdjustments 초기 페이지 호출.
        /// </summary>
        public async Task<CommonResult<SessionInitSnapshot>> InitSessionAsync(
            Dictionary<string, object> data, CancellationToken ct)
        {
            try
            {
                var response = await callFunctionAsync("initSession", data, ct);
                if (response == null)
                    return CommonResult<SessionInitSnapshot>.Failure(
                        COMMON_ERROR_TYPE.COMMON_SERVER,
                        "initSession returned unsupported response.");

                // purchaseAdjustments
                Dictionary<string, object> adjDict = null;
                if (response.TryGetValue("purchaseAdjustments", out var adjObj) && adjObj is Dictionary<string, object> ad)
                    adjDict = ad;

                if (adjDict == null)
                    return CommonResult<SessionInitSnapshot>.Failure(
                        COMMON_ERROR_TYPE.COMMON_SERVER,
                        "initSession: purchaseAdjustments missing.");

                var purchaseAdjustments = parseRefundPageResult(adjDict);

                return CommonResult<SessionInitSnapshot>.Success(
                    new SessionInitSnapshot(purchaseAdjustments));
            }
            catch (OperationCanceledException) { throw; }
            catch (FunctionsException fex)
            {
                switch (fex.ErrorCode)
                {
                    case FunctionsErrorCode.Unavailable:
                    case FunctionsErrorCode.DeadlineExceeded:
                        return CommonResult<SessionInitSnapshot>.Failure(
                            COMMON_ERROR_TYPE.COMMON_NETWORK,
                            "initSession network unavailable.");
                    case FunctionsErrorCode.Unauthenticated:
                    case FunctionsErrorCode.PermissionDenied:
                        return CommonResult<SessionInitSnapshot>.Failure(
                            COMMON_ERROR_TYPE.COMMON_AUTH, fex.Message);
                    default:
                        return CommonResult<SessionInitSnapshot>.Failure(
                            COMMON_ERROR_TYPE.COMMON_SERVER, fex.Message);
                }
            }
            catch (Exception ex)
            {
                return CommonResult<SessionInitSnapshot>.Failure(
                    COMMON_ERROR_TYPE.COMMON_SERVER, ex.Message);
            }
        }

        // ── Purchase Callable — Query ─────────────────────────────

        public async Task<CommonResult<VerifyPurchaseResponse>> VerifyPurchaseAsync(
            Dictionary<string, object> data, CancellationToken ct)
        {
            try
            {
                var response = await callFunctionAsync("verifyPurchase", data, ct);
                if (response == null)
                    return CommonResult<VerifyPurchaseResponse>.Failure(
                        COMMON_ERROR_TYPE.PURCHASE_FUNCTION_RESPONSE_INVALID,
                        "verifyPurchase returned unsupported response type.");

                return CommonResult<VerifyPurchaseResponse>.Success(
                    parseVerifyPurchaseResponse(response));
            }
            catch (OperationCanceledException) { throw; }
            catch (FunctionsException fex)
            {
                return mapPurchaseFunctionsException<VerifyPurchaseResponse>(
                    "verifyPurchase", fex);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[{Tag}] verifyPurchase failed: {ex.Message}");
                return CommonResult<VerifyPurchaseResponse>.Failure(
                    COMMON_ERROR_TYPE.PURCHASE_VERIFY_CALL_FAILED, ex.Message);
            }
        }

        public async Task<CommonResult<EntitlementsSnapshot>> GetEntitlementsAsync(CancellationToken ct)
        {
            try
            {
                var response = await callFunctionAsync("getEntitlements", null, ct);
                if (response == null)
                    return CommonResult<EntitlementsSnapshot>.Failure(
                        COMMON_ERROR_TYPE.PURCHASE_FUNCTION_RESPONSE_INVALID,
                        "getEntitlements returned unsupported response type.");

                return CommonResult<EntitlementsSnapshot>.Success(
                    parseEntitlementsSnapshot(response));
            }
            catch (OperationCanceledException) { throw; }
            catch (FunctionsException fex)
            {
                return mapPurchaseFunctionsException<EntitlementsSnapshot>(
                    "getEntitlements", fex);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[{Tag}] getEntitlements failed: {ex.Message}");
                return CommonResult<EntitlementsSnapshot>.Failure(
                    COMMON_ERROR_TYPE.PURCHASE_ENTITLEMENTS_CALL_FAILED, ex.Message);
            }
        }

        public async Task<CommonResult<RecentPurchaseItem>> GetRecentPurchases30dAsync(
            Dictionary<string, object> data, CancellationToken ct)
        {
            try
            {
                var response = await callFunctionAsync("getRecentPurchases30d", data, ct);
                if (response == null)
                    return CommonResult<RecentPurchaseItem>.Failure(
                        COMMON_ERROR_TYPE.PURCHASE_FUNCTION_RESPONSE_INVALID,
                        "getRecentPurchases30d returned unsupported response type.");

                var item = parseRecentPurchaseItem(response);
                if (item == null)
                    return CommonResult<RecentPurchaseItem>.Failure(
                        COMMON_ERROR_TYPE.PURCHASE_RECENT_NOT_FOUND,
                        "No recent consumable purchase within 30 days.");

                return CommonResult<RecentPurchaseItem>.Success(item);
            }
            catch (OperationCanceledException) { throw; }
            catch (FunctionsException fex)
            {
                return mapPurchaseFunctionsException<RecentPurchaseItem>(
                    "getRecentPurchases30d", fex);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[{Tag}] getRecentPurchases30d failed: {ex.Message}");
                return CommonResult<RecentPurchaseItem>.Failure(
                    COMMON_ERROR_TYPE.PURCHASE_RECENT_CALL_FAILED, ex.Message);
            }
        }

        public async Task<CommonResult<RefundPageResult>> GetPurchaseAdjustmentsAsync(
            Dictionary<string, object> data, CancellationToken ct)
        {
            try
            {
                var response = await callFunctionAsync("getPurchaseAdjustments", data, ct);
                if (response == null)
                    return CommonResult<RefundPageResult>.Failure(
                        COMMON_ERROR_TYPE.PURCHASE_FUNCTION_RESPONSE_INVALID,
                        "getPurchaseAdjustments returned unsupported response type.");

                return CommonResult<RefundPageResult>.Success(
                    parseRefundPageResult(response));
            }
            catch (OperationCanceledException) { throw; }
            catch (FunctionsException fex)
            {
                return mapPurchaseFunctionsException<RefundPageResult>(
                    "getPurchaseAdjustments", fex);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[{Tag}] getPurchaseAdjustments failed: {ex.Message}");
                return CommonResult<RefundPageResult>.Failure(
                    COMMON_ERROR_TYPE.PURCHASE_ADJUSTMENTS_CALL_FAILED, ex.Message);
            }
        }

        // ── Purchase Callable — Ack ───────────────────────────────

        public async Task<CommonResult> AckPurchaseClientGrantAsync(
            Dictionary<string, object> data, CancellationToken ct)
        {
            try
            {
                var response = await callFunctionAsync("ackPurchaseClientGrant", data, ct);
                return CommonResult.Ok();
            }
            catch (OperationCanceledException) { throw; }
            catch (FunctionsException fex)
            {
                return mapPurchaseFunctionsExceptionVoid("ackPurchaseClientGrant", fex);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[{Tag}] ackPurchaseClientGrant failed: {ex.Message}");
                return CommonResult.Failure(
                    COMMON_ERROR_TYPE.PURCHASE_VERIFY_CALL_FAILED, ex.Message);
            }
        }

        public async Task<CommonResult> AckPurchaseStoreConfirmAsync(
            Dictionary<string, object> data, CancellationToken ct)
        {
            try
            {
                var response = await callFunctionAsync("ackPurchaseStoreConfirm", data, ct);
                return CommonResult.Ok();
            }
            catch (OperationCanceledException) { throw; }
            catch (FunctionsException fex)
            {
                return mapPurchaseFunctionsExceptionVoid("ackPurchaseStoreConfirm", fex);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[{Tag}] ackPurchaseStoreConfirm failed: {ex.Message}");
                return CommonResult.Failure(
                    COMMON_ERROR_TYPE.PURCHASE_STORE_CONFIRM_ACK_CALL_FAILED, ex.Message);
            }
        }

        public async Task<CommonResult> AckRefundAppliedAsync(
            Dictionary<string, object> data, CancellationToken ct)
        {
            try
            {
                var response = await callFunctionAsync("ackRefundApplied", data, ct);
                return CommonResult.Ok();
            }
            catch (OperationCanceledException) { throw; }
            catch (FunctionsException fex)
            {
                return mapPurchaseFunctionsExceptionVoid("ackRefundApplied", fex);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[{Tag}] ackRefundApplied failed: {ex.Message}");
                return CommonResult.Failure(
                    COMMON_ERROR_TYPE.PURCHASE_REFUND_APPLY_FAILED, ex.Message);
            }
        }

        // ── Value Extraction Helpers (static) ───────────────────────

        /// <summary>
        /// 지정 키의 값을 long으로 추출한다.
        /// long, int, double, float, string 타입을 처리한다.
        /// 키가 없거나 변환 불가 시 0L 반환.
        /// </summary>
        public static long ReadLong(IDictionary<string, object> source, string key)
        {
            if (source == null || !source.TryGetValue(key, out var raw) || raw == null)
                return 0L;

            switch (raw)
            {
                case long longValue:
                    return longValue;
                case int intValue:
                    return intValue;
                case double doubleValue:
                    return Convert.ToInt64(doubleValue);
                case float floatValue:
                    return Convert.ToInt64(floatValue);
                case string stringValue when long.TryParse(stringValue, out var parsed):
                    return parsed;
                default:
                    return 0L;
            }
        }

        /// <summary>
        /// 지정 키의 값을 string으로 추출한다.
        /// 키가 없거나 null이면 string.Empty 반환.
        /// </summary>
        public static string ReadString(IDictionary<string, object> source, string key)
        {
            if (source == null || !source.TryGetValue(key, out var raw) || raw == null)
                return string.Empty;

            return raw is string s ? s : raw.ToString();
        }

        /// <summary>
        /// 지정 키의 값을 bool로 추출한다.
        /// bool, int, long, double, string 타입을 처리한다.
        /// 키가 없거나 변환 불가 시 false 반환.
        /// </summary>
        public static bool ReadBool(IDictionary<string, object> source, string key)
        {
            if (source == null || !source.TryGetValue(key, out var raw) || raw == null)
                return false;

            if (raw is bool b) return b;
            if (raw is int i) return i != 0;
            if (raw is long l) return l != 0;
            if (raw is double d) return Math.Abs(d) > double.Epsilon;
            if (bool.TryParse(raw.ToString(), out var parsed)) return parsed;
            return false;
        }

        // ── Private — Callable ────────────────────────────────────

        async Task<Dictionary<string, object>> callFunctionAsync(
            string functionName, Dictionary<string, object> data, CancellationToken ct)
        {
            var functions = getFunctionsInstance();
            var callable = functions.GetHttpsCallable(functionName);
            var result = data != null
                ? await callable.CallAsync(data)
                : await callable.CallAsync();

            ct.ThrowIfCancellationRequested();

            return normalizeCallableResponse(result.Data);
        }

        FirebaseFunctions getFunctionsInstance()
        {
            tryInitializeFunctionsRegionFromApplication();

            return string.IsNullOrEmpty(_functionsRegion)
                ? FirebaseFunctions.DefaultInstance
                : FirebaseFunctions.GetInstance(_functionsRegion);
        }

        // ── Private — Error Mapping ───────────────────────────────

        static CommonResult<T> mapPurchaseFunctionsException<T>(string functionName, FunctionsException fex)
        {
            var isVerifyPurchase = functionName == "verifyPurchase";
            var isAckPurchaseClientGrant = functionName == "ackPurchaseClientGrant";
            var isGetPurchaseAdjustments = functionName == "getPurchaseAdjustments";
            var isGetRecentPurchases = functionName == "getRecentPurchases30d";

            COMMON_ERROR_TYPE errorType;
            string message;

            switch (fex.ErrorCode)
            {
                case FunctionsErrorCode.Unauthenticated:
                    errorType = COMMON_ERROR_TYPE.PURCHASE_UNAUTHENTICATED;
                    message = "Authentication required.";
                    break;

                case FunctionsErrorCode.InvalidArgument:
                    if (isVerifyPurchase || isAckPurchaseClientGrant)
                    {
                        errorType = COMMON_ERROR_TYPE.PURCHASE_VERIFY_INVALID_ARGUMENT;
                        message = $"Invalid {functionName} arguments.";
                    }
                    else if (isGetRecentPurchases)
                    {
                        errorType = COMMON_ERROR_TYPE.PURCHASE_RECENT_CALL_FAILED;
                        message = "Invalid getRecentPurchases request arguments.";
                    }
                    else if (isGetPurchaseAdjustments)
                    {
                        errorType = COMMON_ERROR_TYPE.PURCHASE_ADJUSTMENTS_INVALID_ARGUMENT;
                        message = "Invalid getPurchaseAdjustments request arguments.";
                    }
                    else
                    {
                        errorType = mapUnhandledFunctionErrorType(functionName);
                        message = fex.Message;
                    }
                    break;

                case FunctionsErrorCode.FailedPrecondition:
                    if (isVerifyPurchase || isAckPurchaseClientGrant)
                    {
                        errorType = COMMON_ERROR_TYPE.PURCHASE_VERIFY_FAILED_PRECONDITION;
                        message = $"{functionName} failed precondition.";
                    }
                    else
                    {
                        errorType = mapUnhandledFunctionErrorType(functionName);
                        message = fex.Message;
                    }
                    break;

                case FunctionsErrorCode.PermissionDenied:
                    errorType = COMMON_ERROR_TYPE.COMMON_AUTH;
                    message = "Permission denied.";
                    break;

                case FunctionsErrorCode.Unavailable:
                case FunctionsErrorCode.DeadlineExceeded:
                    errorType = mapNetworkFunctionErrorType(functionName);
                    message = "Network unavailable.";
                    break;

                default:
                    errorType = mapUnhandledFunctionErrorType(functionName);
                    message = fex.Message;
                    break;
            }

            Debug.LogWarning(
                $"[{Tag}] {functionName} mapped firebase error: {errorType} " +
                $"(firebase={fex.ErrorCode}) {message}");

            return CommonResult<T>.Failure(errorType, message);
        }

        static CommonResult mapPurchaseFunctionsExceptionVoid(string functionName, FunctionsException fex)
        {
            var isVerifyPurchase = functionName == "verifyPurchase";
            var isAckPurchaseClientGrant = functionName == "ackPurchaseClientGrant";
            var isGetPurchaseAdjustments = functionName == "getPurchaseAdjustments";
            var isGetRecentPurchases = functionName == "getRecentPurchases30d";

            COMMON_ERROR_TYPE errorType;
            string message;

            switch (fex.ErrorCode)
            {
                case FunctionsErrorCode.Unauthenticated:
                    errorType = COMMON_ERROR_TYPE.PURCHASE_UNAUTHENTICATED;
                    message = "Authentication required.";
                    break;

                case FunctionsErrorCode.InvalidArgument:
                    if (isVerifyPurchase || isAckPurchaseClientGrant)
                    {
                        errorType = COMMON_ERROR_TYPE.PURCHASE_VERIFY_INVALID_ARGUMENT;
                        message = $"Invalid {functionName} arguments.";
                    }
                    else if (isGetRecentPurchases)
                    {
                        errorType = COMMON_ERROR_TYPE.PURCHASE_RECENT_CALL_FAILED;
                        message = "Invalid getRecentPurchases request arguments.";
                    }
                    else if (isGetPurchaseAdjustments)
                    {
                        errorType = COMMON_ERROR_TYPE.PURCHASE_ADJUSTMENTS_INVALID_ARGUMENT;
                        message = "Invalid getPurchaseAdjustments request arguments.";
                    }
                    else
                    {
                        errorType = mapUnhandledFunctionErrorType(functionName);
                        message = fex.Message;
                    }
                    break;

                case FunctionsErrorCode.FailedPrecondition:
                    if (isVerifyPurchase || isAckPurchaseClientGrant)
                    {
                        errorType = COMMON_ERROR_TYPE.PURCHASE_VERIFY_FAILED_PRECONDITION;
                        message = $"{functionName} failed precondition.";
                    }
                    else
                    {
                        errorType = mapUnhandledFunctionErrorType(functionName);
                        message = fex.Message;
                    }
                    break;

                case FunctionsErrorCode.PermissionDenied:
                    errorType = COMMON_ERROR_TYPE.COMMON_AUTH;
                    message = "Permission denied.";
                    break;

                case FunctionsErrorCode.Unavailable:
                case FunctionsErrorCode.DeadlineExceeded:
                    errorType = mapNetworkFunctionErrorType(functionName);
                    message = "Network unavailable.";
                    break;

                default:
                    errorType = mapUnhandledFunctionErrorType(functionName);
                    message = fex.Message;
                    break;
            }

            Debug.LogWarning(
                $"[{Tag}] {functionName} mapped firebase error: {errorType} " +
                $"(firebase={fex.ErrorCode}) {message}");

            return CommonResult.Failure(errorType, message);
        }

        static COMMON_ERROR_TYPE mapUnhandledFunctionErrorType(string functionName)
        {
            switch (functionName)
            {
                case "getRecentPurchases30d":
                    return COMMON_ERROR_TYPE.PURCHASE_RECENT_CALL_FAILED;
                case "getPurchaseAdjustments":
                    return COMMON_ERROR_TYPE.PURCHASE_ADJUSTMENTS_CALL_FAILED;
                case "getEntitlements":
                    return COMMON_ERROR_TYPE.PURCHASE_ENTITLEMENTS_CALL_FAILED;
                case "verifyPurchase":
                case "ackPurchaseClientGrant":
                    return COMMON_ERROR_TYPE.PURCHASE_VERIFY_CALL_FAILED;
                case "ackPurchaseStoreConfirm":
                    return COMMON_ERROR_TYPE.PURCHASE_STORE_CONFIRM_ACK_CALL_FAILED;
                case "ackRefundApplied":
                    return COMMON_ERROR_TYPE.PURCHASE_REFUND_APPLY_FAILED;
                default:
                    return COMMON_ERROR_TYPE.PURCHASE_FUNCTION_CALL_FAILED;
            }
        }

        static COMMON_ERROR_TYPE mapNetworkFunctionErrorType(string functionName)
        {
            switch (functionName)
            {
                case "getRecentPurchases30d":
                    return COMMON_ERROR_TYPE.PURCHASE_RECENT_CALL_FAILED;
                case "getPurchaseAdjustments":
                    return COMMON_ERROR_TYPE.PURCHASE_ADJUSTMENTS_CALL_FAILED;
                case "getEntitlements":
                    return COMMON_ERROR_TYPE.PURCHASE_ENTITLEMENTS_CALL_FAILED;
                case "ackPurchaseStoreConfirm":
                    return COMMON_ERROR_TYPE.PURCHASE_STORE_CONFIRM_ACK_CALL_FAILED;
                case "ackRefundApplied":
                    return COMMON_ERROR_TYPE.PURCHASE_REFUND_APPLY_FAILED;
                case "ackPurchaseClientGrant":
                default:
                    return COMMON_ERROR_TYPE.PURCHASE_NETWORK_UNAVAILABLE;
            }
        }

        // ── Private — Response Parsers ────────────────────────────

        static VerifyPurchaseResponse parseVerifyPurchaseResponse(Dictionary<string, object> response)
        {
            var resultStatus = response.TryGetValue("resultStatus", out var rs) ? rs as string ?? "" : "";
            var rejectReason = response.TryGetValue("rejectReason", out var rr) ? rr as string ?? "" : "";
            var purchaseId = response.TryGetValue("purchaseId", out var pid) ? pid as string ?? "" : "";
            var verifyStatus = response.TryGetValue("verifyStatus", out var vs) ? vs as string ?? "" : resultStatus;
            var clientGrantStatus = response.TryGetValue("clientGrantStatus", out var cgs) ? cgs as string ?? "" : "";
            var storeConfirmStatus = response.TryGetValue("storeConfirmStatus", out var scs) ? scs as string ?? "" : "";

            EntitlementsSnapshot? snapshot = null;
            if (response.TryGetValue("entitlementsSnapshot", out var snapObj) && snapObj is Dictionary<string, object> snap)
                snapshot = parseEntitlementsSnapshot(snap);

            return new VerifyPurchaseResponse(
                resultStatus, rejectReason, purchaseId, verifyStatus,
                clientGrantStatus, storeConfirmStatus, snapshot);
        }

        static EntitlementsSnapshot parseEntitlementsSnapshot(Dictionary<string, object> snap)
        {
            // raw season pass IDs (domain 변환 없음 — PurchaseManager가 ResolveSeasonPassId 수행)
            var seasonPasses = new List<string>();
            var seasonPassSet = new HashSet<string>(StringComparer.Ordinal);
            if (snap.TryGetValue("ownedSeasonPasses", out var sp) && sp is IEnumerable<object> spList)
            {
                foreach (var s in spList)
                {
                    if (s is string str && !string.IsNullOrEmpty(str) && seasonPassSet.Add(str))
                        seasonPasses.Add(str);
                }
            }

            var balances = new Dictionary<string, long>();
            if (snap.TryGetValue("currencyBalances", out var cb) && cb is Dictionary<string, object> cbMap)
            {
                foreach (var kv in cbMap)
                    balances[kv.Key] = Convert.ToInt64(kv.Value);
            }

            var rentals = new Dictionary<string, long>();
            if (snap.TryGetValue("rentals", out var rentalsObj) && rentalsObj is Dictionary<string, object> rentalsMap)
            {
                foreach (var kv in rentalsMap)
                    rentals[kv.Key] = Convert.ToInt64(kv.Value);
            }

            var serverNowUtcMs = ReadLong(snap, "serverNowUtcMs");

            return new EntitlementsSnapshot(seasonPasses, balances, rentals, serverNowUtcMs);
        }

        static RecentPurchaseItem parseRecentPurchaseItem(Dictionary<string, object> root)
        {
            if (!root.TryGetValue("items", out var itemsObj)) return null;
            if (!(itemsObj is IList<object> items) || items.Count == 0) return null;
            if (!(items[0] is IDictionary<string, object> first)) return null;

            return new RecentPurchaseItem
            {
                purchaseId = ReadString(first, "purchaseId"),
                internalProductId = ReadString(first, "internalProductId"),
                storePurchasedAtMs = ReadLong(first, "storePurchasedAt"),
                status = ReadString(first, "status"),
            };
        }

        static RefundPageResult parseRefundPageResult(Dictionary<string, object> root)
        {
            // raw server 필드만 파싱 (domain 변환 없음 — PurchaseManager가 수행)
            var items = new List<PurchaseAdjustmentItem>();
            if (root.TryGetValue("items", out var itemsObj) && itemsObj is IList<object> itemList)
            {
                for (var i = 0; i < itemList.Count; i++)
                {
                    if (!(itemList[i] is IDictionary<string, object> item))
                        continue;

                    var purchaseId = ReadString(item, "purchaseId");
                    var internalProductId = ReadString(item, "internalProductId");
                    var resultStatus = ReadString(item, "resultStatus");
                    var reason = ReadString(item, "reason");
                    var kindString = ReadString(item, "kind");
                    var updatedAtUtcMs = ReadLong(item, "updatedAtUtcMs");

                    if (string.IsNullOrEmpty(resultStatus))
                        resultStatus = ReadString(item, "status");
                    if (updatedAtUtcMs <= 0)
                        updatedAtUtcMs = ReadLong(item, "updatedAt");
                    if (updatedAtUtcMs <= 0)
                    {
                        Debug.LogWarning(
                            $"[{Tag}] Skipping refund adjustment with invalid updatedAt. purchaseId={purchaseId}");
                        continue;
                    }

                    items.Add(new PurchaseAdjustmentItem(
                        purchaseId, internalProductId, resultStatus,
                        kindString, reason, updatedAtUtcMs));
                }
            }

            var nextCursor = ReadString(root, "nextCursor");
            var hasMore = ReadBool(root, "hasMore");
            return new RefundPageResult(items.ToArray(), nextCursor, hasMore);
        }

        // ── Private — Response Normalization ──────────────────────

        static Dictionary<string, object> normalizeCallableResponse(object data)
        {
            if (data == null)
                return null;

            if (data is IDictionary<string, object> stringObjectMap)
                return normalizeStringObjectMap(stringObjectMap);

            if (data is IDictionary anyMap)
                return normalizeAnyMap(anyMap);

            return null;
        }

        static Dictionary<string, object> normalizeStringObjectMap(IDictionary<string, object> source)
        {
            var result = new Dictionary<string, object>(source.Count);
            foreach (var kv in source)
                result[kv.Key] = normalizeCallableValue(kv.Value);
            return result;
        }

        static Dictionary<string, object> normalizeAnyMap(IDictionary source)
        {
            var result = new Dictionary<string, object>(source.Count);
            foreach (DictionaryEntry entry in source)
            {
                if (entry.Key == null)
                    continue;

                result[entry.Key.ToString()] = normalizeCallableValue(entry.Value);
            }

            return result;
        }

        static object normalizeCallableValue(object value)
        {
            if (value == null)
                return null;

            if (value is IDictionary<string, object> stringObjectMap)
                return normalizeStringObjectMap(stringObjectMap);

            if (value is IDictionary anyMap)
                return normalizeAnyMap(anyMap);

            if (value is IList list && value is not string)
            {
                var normalized = new List<object>(list.Count);
                foreach (var item in list)
                    normalized.Add(normalizeCallableValue(item));
                return normalized;
            }

            return value;
        }
    }
}
