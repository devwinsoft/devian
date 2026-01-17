using System;
using System.Collections.Generic;
using Devian.Core;
using Google.Protobuf;

namespace Devian.Protobuf
{
    /// <summary>
    /// Protobuf 기반 IEntityConverter 구현체.
    /// 
    /// 책임:
    /// - Entity의 _LoadProto/_SaveProto 메소드만 호출
    /// - Entity 내부 구현을 해석하지 않음
    /// 
    /// 규칙:
    /// - Binary: protobuf 직렬화 (IR 정식 경로)
    /// - JSON: protobuf JSON (레거시/디버그 전용)
    /// - MessageParser&lt;TProto&gt; 주입 필수 (제네릭 static Parser 접근 금지)
    /// 
    /// 위치: Devian.Protobuf (런타임 변환기)
    /// 
    /// 테이블 JSON I/O 정본 (28-json-row-io):
    /// - Load: general JSON(NDJSON) → Descriptor-driven IMessage build → entity._LoadProto()
    /// - Save: entity._SaveProto() → (proto→general JSON 매핑 규칙) → NDJSON string
    /// - ToJson/FromJson은 ProtoJSON 디버그 전용이며, 테이블 정식 I/O에서 사용하지 않음
    /// </summary>
    /// <typeparam name="TEntity">Entity 타입 (IProtoEntity&lt;TProto&gt; 구현 필수)</typeparam>
    /// <typeparam name="TProto">Protobuf 메시지 타입</typeparam>
    public class ProtobufEntityConverter<TEntity, TProto> : IEntityConverter<TEntity>
        where TEntity : class, IProtoEntity<TProto>, new()
        where TProto : IMessage<TProto>, new()
    {
        private readonly MessageParser<TProto> _parser;

        /// <summary>
        /// ProtobufEntityConverter 생성자.
        /// </summary>
        /// <param name="parser">Protobuf MessageParser (필수 주입)</param>
        public ProtobufEntityConverter(MessageParser<TProto> parser)
        {
            _parser = parser ?? throw new ArgumentNullException(nameof(parser));
        }

        /// <summary>
        /// Entity 목록을 Protobuf 바이너리로 변환.
        /// 
        /// 구현:
        /// - 각 entity._SaveProto() 호출
        /// - proto.ToByteArray() 실행
        /// - 길이 prefix 방식으로 연결
        /// </summary>
        public byte[] ToBinary(IReadOnlyList<TEntity> entities)
        {
            if (entities == null || entities.Count == 0)
                return Array.Empty<byte>();

            using var stream = new System.IO.MemoryStream();
            foreach (var entity in entities)
            {
                var proto = entity._SaveProto();
                proto.WriteDelimitedTo(stream);
            }
            return stream.ToArray();
        }

        /// <summary>
        /// Protobuf 바이너리에서 Entity 목록으로 변환.
        /// 
        /// 구현:
        /// - MessageParser&lt;TProto&gt;.ParseDelimitedFrom() 사용
        /// - new TEntity() 생성
        /// - entity._LoadProto(msg) 호출
        /// </summary>
        public IReadOnlyList<TEntity> FromBinary(byte[] bytes)
        {
            if (bytes == null || bytes.Length == 0)
                return Array.Empty<TEntity>();

            var result = new List<TEntity>();
            using var stream = new System.IO.MemoryStream(bytes);

            while (stream.Position < stream.Length)
            {
                var msg = _parser.ParseDelimitedFrom(stream);
                var entity = new TEntity();
                entity._LoadProto(msg);
                result.Add(entity);
            }

            return result;
        }

        /// <summary>
        /// [NOT USED IN TABLE I/O] Entity 목록을 Protobuf JSON으로 변환.
        /// 
        /// 레거시/디버그 전용. 테이블 Save는 컨테이너.SaveToJson() 사용.
        /// 
        /// 구현:
        /// - 각 entity._SaveJson() 호출
        /// - JSON 배열로 조합
        /// </summary>
        public string ToJson(IReadOnlyList<TEntity> entities)
        {
            if (entities == null || entities.Count == 0)
                return "[]";

            var sb = new System.Text.StringBuilder();
            sb.Append('[');

            for (int i = 0; i < entities.Count; i++)
            {
                if (i > 0) sb.Append(',');
                sb.Append(entities[i]._SaveJson());
            }

            sb.Append(']');
            return sb.ToString();
        }

        /// <summary>
        /// [NOT USED IN TABLE I/O] Protobuf JSON에서 Entity 목록으로 변환.
        /// 
        /// 레거시/디버그 전용. 테이블 Load는 컨테이너.LoadFromJson(ndjson) 사용.
        /// (이 메서드는 ProtoJSON 배열 파싱용이며, 테이블 정본 파이프라인이 아님)
        /// 
        /// 구현:
        /// - JSON 배열 파싱
        /// - 각 요소에 대해 entity._LoadJson(json) 호출
        /// </summary>
        public IReadOnlyList<TEntity> FromJson(string json)
        {
            if (string.IsNullOrWhiteSpace(json) || json == "[]")
                return Array.Empty<TEntity>();

            var result = new List<TEntity>();

            // JSON 배열 파싱 (간단한 구현)
            var elements = parseJsonArray(json);
            foreach (var element in elements)
            {
                var entity = new TEntity();
                entity._LoadJson(element);
                result.Add(entity);
            }

            return result;
        }

        /// <summary>
        /// JSON 배열을 개별 요소로 분리.
        /// </summary>
        private static List<string> parseJsonArray(string json)
        {
            var result = new List<string>();
            var trimmed = json.Trim();

            if (!trimmed.StartsWith("[") || !trimmed.EndsWith("]"))
                throw new FormatException("Invalid JSON array format");

            // 대괄호 제거
            var content = trimmed.Substring(1, trimmed.Length - 2).Trim();
            if (string.IsNullOrEmpty(content))
                return result;

            // 중괄호 깊이 추적하며 분리
            int depth = 0;
            int start = 0;

            for (int i = 0; i < content.Length; i++)
            {
                char c = content[i];
                if (c == '{') depth++;
                else if (c == '}') depth--;
                else if (c == ',' && depth == 0)
                {
                    result.Add(content.Substring(start, i - start).Trim());
                    start = i + 1;
                }
            }

            // 마지막 요소
            if (start < content.Length)
            {
                result.Add(content.Substring(start).Trim());
            }

            return result;
        }
    }
}
