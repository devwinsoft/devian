import { IEntity } from 'devian-core';

/**
 * Protobuf 직렬화를 지원하는 Entity 계약.
 * 
 * 정식 경로:
 * - loadProto/saveProto: 필드 단위 decode/encode
 * - Binary I/O: toBinary/fromBinary
 * 
 * 디버그/레거시 경로:
 * - loadJson/saveJson: 디버그 전용 protobuf JSON 입출력
 * 
 * @see Devian.Protobuf.IProtoEntity (C#)
 */
export interface IProtoEntity<TProto = unknown> extends IEntity {
    /**
     * Protobuf 메시지에서 엔티티 필드 로드.
     * 의미 변환(암호화 복호화 등)의 중심.
     * 테이블 JSON Load 파이프라인의 마지막 단계.
     */
    _loadProto(msg: TProto): void;

    /**
     * 엔티티 필드를 Protobuf 메시지로 저장.
     * 의미 변환(암호화 등)의 중심.
     * 테이블 JSON Save 파이프라인의 시작 단계.
     */
    _saveProto(): TProto;

    /**
     * [NOT USED IN TABLE I/O] Protobuf JSON 문자열에서 엔티티 로드.
     * 디버그/테스트 전용.
     */
    _loadJson?(json: string): void;

    /**
     * [NOT USED IN TABLE I/O] 엔티티를 Protobuf JSON 문자열로 저장.
     * 디버그/테스트 전용.
     */
    _saveJson?(): string;
}
