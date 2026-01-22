using Devian.Core;

namespace Devian.Protobuf
{
    /// <summary>
    /// Protobuf 직렬화를 지원하는 Entity 계약.
    /// 
    /// 정식 경로:
    /// - _LoadProto/_SaveProto: 필드 단위 decode/encode (암호화·복호화 포함)
    /// - Binary I/O: IEntityConverter.ToBinary/FromBinary
    /// 
    /// 디버그/레거시 경로:
    /// - _LoadJson/_SaveJson: 디버그 전용 protobuf JSON 입출력
    /// 
    /// 테이블 JSON I/O 정본 (28-json-row-io):
    /// - Load: general JSON(NDJSON) → Descriptor-driven IMessage build → entity._LoadProto()
    /// - Save: entity._SaveProto() → (proto→general JSON 매핑 규칙) → NDJSON string
    /// - _LoadJson/_SaveJson은 ProtoJSON 디버그 전용이며, 테이블 정식 I/O에서 사용하지 않음
    /// 
    /// 위치: Devian.Protobuf (Core는 Protobuf를 참조하지 않음)
    /// </summary>
    /// <typeparam name="TProto">Protobuf 메시지 타입</typeparam>
    public interface IProtoEntity<TProto> : IEntity
        where TProto : Google.Protobuf.IMessage<TProto>
    {
        /// <summary>
        /// Protobuf 메시지에서 엔티티 필드 로드.
        /// 의미 변환(암호화 복호화 등)의 중심.
        /// 테이블 JSON Load 파이프라인의 마지막 단계.
        /// </summary>
        void _LoadProto(TProto msg);

        /// <summary>
        /// 엔티티 필드를 Protobuf 메시지로 저장.
        /// 의미 변환(암호화 등)의 중심.
        /// 테이블 JSON Save 파이프라인의 시작 단계.
        /// </summary>
        TProto _SaveProto();

        /// <summary>
        /// [NOT USED IN TABLE I/O] Protobuf JSON 문자열에서 엔티티 로드.
        /// 내부에서 JsonParser로 TProto 생성 후 _LoadProto 호출.
        /// 
        /// 디버그/테스트 전용. 테이블 Load는 컨테이너.LoadFromJson(ndjson) 사용.
        /// </summary>
        void _LoadJson(string json);

        /// <summary>
        /// [NOT USED IN TABLE I/O] 엔티티를 Protobuf JSON 문자열로 저장.
        /// 내부에서 _SaveProto 호출 후 JsonFormatter로 변환.
        /// 
        /// 디버그/테스트 전용. 테이블 Save는 컨테이너.SaveToJson() 사용.
        /// </summary>
        string _SaveJson();
    }
}
