using System;
using System.Collections.Generic;

namespace Devian
{
    /// <summary>
    /// DFF(Devian Friendly Format) 파싱 결과의 중간 표현.
    /// 
    /// 모든 DFF 값은 아래 4가지 중 하나로 표현된다:
    /// - Unset: 빈 값, null, NULL, ~, - 등
    /// - Scalar: 원시 문자열 (타입 변환 전)
    /// - List: 반복 값 목록
    /// - Object: key-value 쌍 목록
    /// 
    /// 주의: 이 타입은 Proto Descriptor 기반 타입 변환 전의 "원시 구조"만 표현한다.
    /// 실제 타입 변환은 DffProtobufBuilder에서 수행한다.
    /// </summary>
    public sealed class DffValue
    {
        /// <summary>
        /// DFF 값의 종류
        /// </summary>
        public enum Kind
        {
            /// <summary>빈 값, null, NULL, ~, - 등</summary>
            Unset,
            /// <summary>원시 문자열 값 (타입 변환 전)</summary>
            Scalar,
            /// <summary>반복 값 목록 [a, b, c]</summary>
            List,
            /// <summary>key-value 쌍 목록 (id=1; name=A)</summary>
            Object
        }

        /// <summary>값의 종류</summary>
        public Kind ValueKind { get; }

        /// <summary>Scalar 값 (ValueKind == Scalar일 때만 유효)</summary>
        public string? ScalarValue { get; }

        /// <summary>List 값 (ValueKind == List일 때만 유효)</summary>
        public IReadOnlyList<DffValue>? ListValue { get; }

        /// <summary>Object 값 (ValueKind == Object일 때만 유효)</summary>
        public IReadOnlyDictionary<string, DffValue>? ObjectValue { get; }

        private DffValue(Kind kind, string? scalar, IReadOnlyList<DffValue>? list, IReadOnlyDictionary<string, DffValue>? obj)
        {
            ValueKind = kind;
            ScalarValue = scalar;
            ListValue = list;
            ObjectValue = obj;
        }

        /// <summary>Unset 값 (싱글톤)</summary>
        public static readonly DffValue Unset = new DffValue(Kind.Unset, null, null, null);

        /// <summary>Scalar 값 생성</summary>
        public static DffValue FromScalar(string value)
        {
            if (value == null)
                throw new ArgumentNullException(nameof(value));
            return new DffValue(Kind.Scalar, value, null, null);
        }

        /// <summary>List 값 생성</summary>
        public static DffValue FromList(IReadOnlyList<DffValue> items)
        {
            if (items == null)
                throw new ArgumentNullException(nameof(items));
            return new DffValue(Kind.List, null, items, null);
        }

        /// <summary>Object 값 생성</summary>
        public static DffValue FromObject(IReadOnlyDictionary<string, DffValue> fields)
        {
            if (fields == null)
                throw new ArgumentNullException(nameof(fields));
            return new DffValue(Kind.Object, null, null, fields);
        }

        /// <summary>Unset 여부</summary>
        public bool IsUnset => ValueKind == Kind.Unset;

        /// <summary>Scalar 여부</summary>
        public bool IsScalar => ValueKind == Kind.Scalar;

        /// <summary>List 여부</summary>
        public bool IsList => ValueKind == Kind.List;

        /// <summary>Object 여부</summary>
        public bool IsObject => ValueKind == Kind.Object;

        /// <summary>디버그용 문자열 표현</summary>
        public override string ToString()
        {
            return ValueKind switch
            {
                Kind.Unset => "(unset)",
                Kind.Scalar => $"\"{ScalarValue}\"",
                Kind.List => $"[{ListValue?.Count ?? 0} items]",
                Kind.Object => $"{{{ObjectValue?.Count ?? 0} fields}}",
                _ => "(unknown)"
            };
        }
    }
}
