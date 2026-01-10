#nullable enable

namespace Devian.Core
{
    /// <summary>
    /// 테이블 컨테이너 인터페이스.
    /// 모든 생성된 TB_{TableName} 타입이 구현한다.
    /// </summary>
    /// <remarks>
    /// - JSON은 NDJSON string (1 row = 1 line). Root array 금지.
    /// - Base64는 내부 binary(delimited) + base64 wrapper.
    /// </remarks>
    public interface ITableContainer
    {
        /// <summary>
        /// 캐시된 모든 Row를 제거한다.
        /// </summary>
        void Clear();

        /// <summary>
        /// NDJSON string에서 Row들을 로드한다.
        /// </summary>
        /// <param name="json">NDJSON string (1 row = 1 line)</param>
        /// <param name="mode">Merge(기본): key 충돌 시 덮어씀. Replace: Clear 후 적재.</param>
        void LoadFromJson(string json, LoadMode mode = LoadMode.Merge);

        /// <summary>
        /// 캐시된 Row들을 NDJSON string으로 반환한다.
        /// </summary>
        /// <returns>NDJSON string (1 row = 1 line)</returns>
        string SaveToJson();

        /// <summary>
        /// Base64 인코딩된 binary에서 Row들을 로드한다.
        /// </summary>
        /// <param name="base64">Base64 인코딩된 delimited binary</param>
        /// <param name="mode">Merge(기본): key 충돌 시 덮어씀. Replace: Clear 후 적재.</param>
        void LoadFromBase64(string base64, LoadMode mode = LoadMode.Merge);

        /// <summary>
        /// 캐시된 Row들을 Base64 인코딩된 binary로 반환한다.
        /// </summary>
        /// <returns>Base64 인코딩된 delimited binary</returns>
        string SaveToBase64();
    }
}
