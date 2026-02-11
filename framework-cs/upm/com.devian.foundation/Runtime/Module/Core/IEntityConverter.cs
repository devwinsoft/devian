using System.Collections.Generic;

namespace Devian
{
    /// <summary>
    /// Entity 직렬화/역직렬화 계약.
    /// 
    /// IR 경로 (Protobuf Binary):
    /// - ToBinary / FromBinary - 정식 직렬화 경로
    /// 
    /// JSON I/O:
    /// - ToJson / FromJson - 레거시/디버그용. 테이블 정식 I/O에서 사용하지 않음.
    /// - 테이블 JSON I/O는 28-json-row-io 파이프라인 참조:
    ///   Load: general JSON(NDJSON) → Descriptor-driven IMessage build → entity._LoadProto()
    ///   Save: entity._SaveProto() → (proto→general JSON 매핑 규칙) → NDJSON string
    /// </summary>
    /// <typeparam name="TEntity">Entity 타입</typeparam>
    public interface IEntityConverter<TEntity>
    {
        // ======================================
        // IR 경로 (Protobuf Binary) - 정식 경로
        // ======================================
        
        /// <summary>
        /// Entity 목록을 바이너리로 변환.
        /// </summary>
        byte[] ToBinary(IReadOnlyList<TEntity> entities);

        /// <summary>
        /// 바이너리에서 Entity 목록으로 변환.
        /// </summary>
        IReadOnlyList<TEntity> FromBinary(byte[] bytes);

        // ======================================
        // [NOT USED IN TABLE I/O] Legacy/Debug
        // 테이블 JSON I/O는 28-json-row-io 파이프라인 사용
        // ======================================
        
        /// <summary>
        /// [NOT USED IN TABLE I/O] Entity 목록을 JSON 문자열로 변환.
        /// 테이블 Save는 _SaveProto() + 28 매핑 규칙 사용.
        /// </summary>
        string ToJson(IReadOnlyList<TEntity> entities);

        /// <summary>
        /// [NOT USED IN TABLE I/O] JSON 문자열에서 Entity 목록으로 변환.
        /// 테이블 Load는 NDJSON → IMessage build → _LoadProto() 사용.
        /// </summary>
        IReadOnlyList<TEntity> FromJson(string json);
    }
}
