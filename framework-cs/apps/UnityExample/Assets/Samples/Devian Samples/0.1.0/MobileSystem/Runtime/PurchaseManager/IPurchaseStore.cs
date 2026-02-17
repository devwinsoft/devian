namespace Devian
{
    /// <summary>
    /// Option B: public interface는 PurchaseManager 샘플(패키지)에서 소유한다.
    /// 스토어별 차이(서버 verify 시 store 키, payload 구성)를 분리한다.
    /// Unity IAP 5.x v5 API 기준 — [Obsolete] API 사용하지 않음.
    /// </summary>
    public interface IPurchaseStore
    {
        /// <summary>서버 verify 요청 시 사용하는 스토어 식별자 ("apple" / "google").</summary>
        string StoreKey { get; }

        /// <summary>
        /// PendingOrder의 receipt(JSON)로부터 서버 verify에 보낼 payload를 구성한다.
        /// 기본 구현은 receipt 원본을 그대로 반환하지만,
        /// 스토어별로 필요한 필드만 추출하거나 가공할 수 있다.
        /// </summary>
        string BuildVerifyPayload(string receipt);
    }
}
