using System;

namespace Devian
{
    /// <summary>
    /// DFF 파싱 및 IMessage 변환 옵션.
    /// 
    /// 기본 정책 (MUST):
    /// - Unknown key: 에러
    /// - 타입 변환 실패: 에러
    /// - 범위 초과: 에러
    /// - oneof 충돌: 에러
    /// 
    /// Dev mode에서만 완화 옵션 사용 가능.
    /// </summary>
    public sealed class DffOptions
    {
        /// <summary>
        /// 기본 옵션 (엄격 모드)
        /// </summary>
        public static readonly DffOptions Default = new DffOptions();

        /// <summary>
        /// Dev 모드 옵션 (완화된 검증)
        /// </summary>
        public static readonly DffOptions DevMode = new DffOptions
        {
            AllowUnknownFields = true,
            AllowEnumNumbers = true,
            AllowJsonFormat = true
        };

        /// <summary>
        /// Unknown field 허용 여부.
        /// 기본: false (오타 방지를 위해 에러)
        /// </summary>
        public bool AllowUnknownFields { get; set; } = false;

        /// <summary>
        /// enum 값에 숫자 허용 여부.
        /// 기본: false (이름만 허용)
        /// </summary>
        public bool AllowEnumNumbers { get; set; } = false;

        /// <summary>
        /// JSON object/array 포맷 허용 여부.
        /// 기본: false (DFF pair-list만 허용)
        /// </summary>
        public bool AllowJsonFormat { get; set; } = false;

        /// <summary>
        /// NaN/Inf 허용 여부 (float/double).
        /// 기본: false
        /// </summary>
        public bool AllowNanInf { get; set; } = false;

        /// <summary>
        /// int64/uint64에서 2^53-1 초과 값 허용 여부.
        /// 기본: false (Excel 숫자 셀 정밀도 손실 방지)
        /// </summary>
        public bool AllowLargeInt64 { get; set; } = false;

        /// <summary>
        /// Timestamp 파싱 시 기본 timezone.
        /// timezone 없는 입력에 적용됨.
        /// 기본: Asia/Seoul (+09:00)
        /// </summary>
        public TimeSpan DefaultTimezoneOffset { get; set; } = TimeSpan.FromHours(9);

        /// <summary>
        /// 범위 검증 실패 시 에러 여부.
        /// 기본: true (byte/short 등 범위 초과 시 에러)
        /// </summary>
        public bool StrictRangeValidation { get; set; } = true;

        /// <summary>
        /// oneof 충돌 시 에러 여부.
        /// 기본: true (2개 이상 set 시 에러)
        /// </summary>
        public bool StrictOneofValidation { get; set; } = true;

        /// <summary>
        /// 옵션 복사
        /// </summary>
        public DffOptions Clone()
        {
            return new DffOptions
            {
                AllowUnknownFields = AllowUnknownFields,
                AllowEnumNumbers = AllowEnumNumbers,
                AllowJsonFormat = AllowJsonFormat,
                AllowNanInf = AllowNanInf,
                AllowLargeInt64 = AllowLargeInt64,
                DefaultTimezoneOffset = DefaultTimezoneOffset,
                StrictRangeValidation = StrictRangeValidation,
                StrictOneofValidation = StrictOneofValidation
            };
        }
    }
}
