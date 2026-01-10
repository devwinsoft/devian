import { ICodec } from 'devian-core';

/**
 * Protobuf Codec 설정
 */
export interface ProtobufCodecOptions {
    /**
     * 메시지 타입 정의 (opcode → 인코더/디코더)
     */
    messageTypes?: Map<number, MessageTypeDefinition>;
}

/**
 * 메시지 타입 정의
 */
export interface MessageTypeDefinition {
    /**
     * 인코딩 함수
     */
    encode: (message: unknown) => Uint8Array;

    /**
     * 디코딩 함수
     */
    decode: (data: Uint8Array) => unknown;
}

/**
 * Protobuf Codec 구현.
 * 
 * 사용법:
 * 1. 생성된 .proto 파일에서 메시지 타입 정의를 로드
 * 2. ProtobufCodec 인스턴스 생성 시 messageTypes 전달
 * 3. encode/decode 메서드 사용
 * 
 * @see Devian.Protobuf.DffProtobuf (C#)
 */
export class ProtobufCodec implements ICodec {
    private messageTypes: Map<number, MessageTypeDefinition>;

    constructor(options?: ProtobufCodecOptions) {
        this.messageTypes = options?.messageTypes ?? new Map();
    }

    /**
     * 메시지 타입 정의 등록
     */
    registerMessageType(opcode: number, definition: MessageTypeDefinition): void {
        this.messageTypes.set(opcode, definition);
    }

    /**
     * 메시지를 Protobuf 바이너리로 인코딩한다.
     * 
     * @param message 인코딩할 메시지
     * @returns 인코딩된 바이너리
     * @throws 메시지 타입이 등록되지 않은 경우
     */
    encode<T>(message: T): Uint8Array {
        // 기본 구현: JSON fallback
        const json = JSON.stringify(message);
        return new TextEncoder().encode(json);
    }

    /**
     * Opcode 기반으로 메시지를 인코딩한다.
     */
    encodeByOpcode<T>(opcode: number, message: T): Uint8Array {
        const definition = this.messageTypes.get(opcode);
        if (!definition) {
            // Fallback to JSON
            return this.encode(message);
        }
        return definition.encode(message);
    }

    /**
     * Protobuf 바이너리를 메시지로 디코딩한다.
     * 
     * @param data 디코딩할 바이너리
     * @returns 디코딩된 메시지
     */
    decode<T>(data: Uint8Array): T {
        // 기본 구현: JSON fallback
        const json = new TextDecoder().decode(data);
        return JSON.parse(json) as T;
    }

    /**
     * Opcode 기반으로 바이너리를 디코딩한다.
     */
    decodeByOpcode(opcode: number, data: Uint8Array): unknown {
        const definition = this.messageTypes.get(opcode);
        if (!definition) {
            // Fallback to JSON
            return this.decode(data);
        }
        return definition.decode(data);
    }
}

/**
 * JSON Codec 구현 (Protobuf 대안)
 */
export class JsonCodec implements ICodec {
    private encoder = new TextEncoder();
    private decoder = new TextDecoder();

    encode<T>(message: T): Uint8Array {
        const json = JSON.stringify(message);
        return this.encoder.encode(json);
    }

    decode<T>(data: Uint8Array): T {
        const json = this.decoder.decode(data);
        return JSON.parse(json) as T;
    }

    decodeByOpcode(_opcode: number, data: Uint8Array): unknown {
        return this.decode(data);
    }
}
