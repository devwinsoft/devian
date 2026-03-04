using System;

namespace Devian
{
    /// <summary>
    /// Firebase getPurchaseAdjustments 응답의 raw 페이지 결과.
    /// domain 변환은 포함하지 않는다.
    /// </summary>
    public readonly struct RefundPageResult
    {
        public RefundPageResult(PurchaseAdjustmentItem[] items, string nextCursor, bool hasMore)
        {
            Items = items ?? Array.Empty<PurchaseAdjustmentItem>();
            NextCursor = nextCursor ?? string.Empty;
            HasMore = hasMore;
        }

        public PurchaseAdjustmentItem[] Items { get; }
        public string NextCursor { get; }
        public bool HasMore { get; }
    }
}
