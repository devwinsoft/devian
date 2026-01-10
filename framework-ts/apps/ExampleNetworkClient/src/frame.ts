/**
 * Frame parsing utilities for client
 * 
 * Frame format: [int32 LE opcode][payload bytes]
 */

/** Parsed frame */
export interface ParsedFrame {
    opcode: number;
    payload: Uint8Array;
}

/** Minimum frame size */
const MIN_FRAME_SIZE = 4;

/**
 * Parse frame from raw bytes
 * @returns Parsed frame or null if invalid
 */
export function parseFrame(data: ArrayBuffer | Uint8Array): ParsedFrame | null {
    const bytes = data instanceof Uint8Array ? data : new Uint8Array(data);
    
    if (bytes.length < MIN_FRAME_SIZE) {
        return null;
    }

    const view = new DataView(bytes.buffer, bytes.byteOffset, bytes.byteLength);
    const opcode = view.getInt32(0, true); // little-endian
    const payload = bytes.slice(4);

    return { opcode, payload };
}
