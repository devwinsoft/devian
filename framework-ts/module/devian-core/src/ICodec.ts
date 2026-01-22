/**
 * Codec interface for message serialization.
 * Protocol 메시지 직렬화/역직렬화 인터페이스.
 */
export interface ICodec {
    /**
     * 메시지를 바이너리로 인코딩한다.
     * @param message 인코딩할 메시지
     * @returns 인코딩된 바이너리
     */
    encode<T>(message: T): Uint8Array;

    /**
     * 바이너리를 메시지로 디코딩한다.
     * @param data 디코딩할 바이너리
     * @returns 디코딩된 메시지
     */
    decode<T>(data: Uint8Array): T;

    /**
     * Opcode 기반으로 바이너리를 메시지로 디코딩한다.
     * @param opcode 메시지 Opcode
     * @param data 디코딩할 바이너리
     * @returns 디코딩된 메시지
     */
    decodeByOpcode(opcode: number, data: Uint8Array): unknown;
}
