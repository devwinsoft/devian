/**
 * Tagged BigInt JSON Codec
 * 
 * Handles bigint serialization using tagged format: { $bigint: "123" }
 */

/** Codec interface */
export interface ICodec {
    encode<T>(message: T): Uint8Array;
    encodeByOpcode<T>(opcode: number, message: T): Uint8Array;
    decode<T>(data: Uint8Array): T;
    decodeByOpcode(opcode: number, data: Uint8Array): unknown;
}

/**
 * Tagged BigInt JSON Codec
 * 
 * - Serializes bigint as { $bigint: "value" }
 * - Deserializes $bigint tags back to BigInt
 */
export class TaggedBigIntCodec implements ICodec {
    private encoder = new TextEncoder();
    private decoder = new TextDecoder();

    /**
     * Stringify with bigint → { $bigint: "..." } conversion
     */
    private stringify(value: unknown): string {
        return JSON.stringify(value, (_key, val) => {
            if (typeof val === 'bigint') {
                return { $bigint: val.toString() };
            }
            return val;
        });
    }

    /**
     * Parse with { $bigint: "..." } → BigInt restoration
     */
    private parse(json: string): unknown {
        return JSON.parse(json, (_key, val) => {
            if (val && typeof val === 'object' && '$bigint' in val) {
                return BigInt(val.$bigint);
            }
            return val;
        });
    }

    encode<T>(message: T): Uint8Array {
        const json = this.stringify(message);
        return this.encoder.encode(json);
    }

    encodeByOpcode<T>(_opcode: number, message: T): Uint8Array {
        return this.encode(message);
    }

    decode<T>(data: Uint8Array): T {
        const json = this.decoder.decode(data);
        return this.parse(json) as T;
    }

    decodeByOpcode(_opcode: number, data: Uint8Array): unknown {
        return this.decode(data);
    }
}

/** Default codec instance */
export const defaultCodec = new TaggedBigIntCodec();
