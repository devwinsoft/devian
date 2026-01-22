// SSOT: skills/devian-common/13-feature-complex/SKILL.md

import { error } from './logger';

// ============================================================================
// Encryption Tables (same as C#)
// ============================================================================

const ENCRYPT_TABLE: readonly number[] = [
    0x63, 0x7c, 0x77, 0x7b, 0xf2, 0x6b, 0x6f, 0xc5, 0x30, 0x01, 0x67, 0x2b, 0xfe, 0xd7, 0xab, 0x76,
    0xca, 0x82, 0xc9, 0x7d, 0xfa, 0x59, 0x47, 0xf0, 0xad, 0xd4, 0xa2, 0xaf, 0x9c, 0xa4, 0x72, 0xc0,
    0xb7, 0xfd, 0x93, 0x26, 0x36, 0x3f, 0xf7, 0xcc, 0x34, 0xa5, 0xe5, 0xf1, 0x71, 0xd8, 0x31, 0x15,
    0x04, 0xc7, 0x23, 0xc3, 0x18, 0x96, 0x05, 0x9a, 0x07, 0x12, 0x80, 0xe2, 0xeb, 0x27, 0xb2, 0x75,
    0x09, 0x83, 0x2c, 0x1a, 0x1b, 0x6e, 0x5a, 0xa0, 0x52, 0x3b, 0xd6, 0xb3, 0x29, 0xe3, 0x2f, 0x84,
    0x53, 0xd1, 0x00, 0xed, 0x20, 0xfc, 0xb1, 0x5b, 0x6a, 0xcb, 0xbe, 0x39, 0x4a, 0x4c, 0x58, 0xcf,
    0xd0, 0xef, 0xaa, 0xfb, 0x43, 0x4d, 0x33, 0x85, 0x45, 0xf9, 0x02, 0x7f, 0x50, 0x3c, 0x9f, 0xa8,
    0x51, 0xa3, 0x40, 0x8f, 0x92, 0x9d, 0x38, 0xf5, 0xbc, 0xb6, 0xda, 0x21, 0x10, 0xff, 0xf3, 0xd2,
    0xcd, 0x0c, 0x13, 0xec, 0x5f, 0x97, 0x44, 0x17, 0xc4, 0xa7, 0x7e, 0x3d, 0x64, 0x5d, 0x19, 0x73,
    0x60, 0x81, 0x4f, 0xdc, 0x22, 0x2a, 0x90, 0x88, 0x46, 0xee, 0xb8, 0x14, 0xde, 0x5e, 0x0b, 0xdb,
    0xe0, 0x32, 0x3a, 0x0a, 0x49, 0x06, 0x24, 0x5c, 0xc2, 0xd3, 0xac, 0x62, 0x91, 0x95, 0xe4, 0x79,
    0xe7, 0xc8, 0x37, 0x6d, 0x8d, 0xd5, 0x4e, 0xa9, 0x6c, 0x56, 0xf4, 0xea, 0x65, 0x7a, 0xae, 0x08,
    0xba, 0x78, 0x25, 0x2e, 0x1c, 0xa6, 0xb4, 0xc6, 0xe8, 0xdd, 0x74, 0x1f, 0x4b, 0xbd, 0x8b, 0x8a,
    0x70, 0x3e, 0xb5, 0x66, 0x48, 0x03, 0xf6, 0x0e, 0x61, 0x35, 0x57, 0xb9, 0x86, 0xc1, 0x1d, 0x9e,
    0xe1, 0xf8, 0x98, 0x11, 0x69, 0xd9, 0x8e, 0x94, 0x9b, 0x1e, 0x87, 0xe9, 0xce, 0x55, 0x28, 0xdf,
    0x8c, 0xa1, 0x89, 0x0d, 0xbf, 0xe6, 0x42, 0x68, 0x41, 0x99, 0x2d, 0x0f, 0xb0, 0x54, 0xbb, 0x16
];

const DECRYPT_TABLE: readonly number[] = [
    0x52, 0x09, 0x6a, 0xd5, 0x30, 0x36, 0xa5, 0x38, 0xbf, 0x40, 0xa3, 0x9e, 0x81, 0xf3, 0xd7, 0xfb,
    0x7c, 0xe3, 0x39, 0x82, 0x9b, 0x2f, 0xff, 0x87, 0x34, 0x8e, 0x43, 0x44, 0xc4, 0xde, 0xe9, 0xcb,
    0x54, 0x7b, 0x94, 0x32, 0xa6, 0xc2, 0x23, 0x3d, 0xee, 0x4c, 0x95, 0x0b, 0x42, 0xfa, 0xc3, 0x4e,
    0x08, 0x2e, 0xa1, 0x66, 0x28, 0xd9, 0x24, 0xb2, 0x76, 0x5b, 0xa2, 0x49, 0x6d, 0x8b, 0xd1, 0x25,
    0x72, 0xf8, 0xf6, 0x64, 0x86, 0x68, 0x98, 0x16, 0xd4, 0xa4, 0x5c, 0xcc, 0x5d, 0x65, 0xb6, 0x92,
    0x6c, 0x70, 0x48, 0x50, 0xfd, 0xed, 0xb9, 0xda, 0x5e, 0x15, 0x46, 0x57, 0xa7, 0x8d, 0x9d, 0x84,
    0x90, 0xd8, 0xab, 0x00, 0x8c, 0xbc, 0xd3, 0x0a, 0xf7, 0xe4, 0x58, 0x05, 0xb8, 0xb3, 0x45, 0x06,
    0xd0, 0x2c, 0x1e, 0x8f, 0xca, 0x3f, 0x0f, 0x02, 0xc1, 0xaf, 0xbd, 0x03, 0x01, 0x13, 0x8a, 0x6b,
    0x3a, 0x91, 0x11, 0x41, 0x4f, 0x67, 0xdc, 0xea, 0x97, 0xf2, 0xcf, 0xce, 0xf0, 0xb4, 0xe6, 0x73,
    0x96, 0xac, 0x74, 0x22, 0xe7, 0xad, 0x35, 0x85, 0xe2, 0xf9, 0x37, 0xe8, 0x1c, 0x75, 0xdf, 0x6e,
    0x47, 0xf1, 0x1a, 0x71, 0x1d, 0x29, 0xc5, 0x89, 0x6f, 0xb7, 0x62, 0x0e, 0xaa, 0x18, 0xbe, 0x1b,
    0xfc, 0x56, 0x3e, 0x4b, 0xc6, 0xd2, 0x79, 0x20, 0x9a, 0xdb, 0xc0, 0xfe, 0x78, 0xcd, 0x5a, 0xf4,
    0x1f, 0xdd, 0xa8, 0x33, 0x88, 0x07, 0xc7, 0x31, 0xb1, 0x12, 0x10, 0x59, 0x27, 0x80, 0xec, 0x5f,
    0x60, 0x51, 0x7f, 0xa9, 0x19, 0xb5, 0x4a, 0x0d, 0x2d, 0xe5, 0x7a, 0x9f, 0x93, 0xc9, 0x9c, 0xef,
    0xa0, 0xe0, 0x3b, 0x4d, 0xae, 0x2a, 0xf5, 0xb0, 0xc8, 0xeb, 0xbb, 0x3c, 0x83, 0x53, 0x99, 0x61,
    0x17, 0x2b, 0x04, 0x7e, 0xba, 0x77, 0xd6, 0x26, 0xe1, 0x69, 0x14, 0x63, 0x55, 0x21, 0x0c, 0x7d
];

// ============================================================================
// ComplexUtil
// ============================================================================

/**
 * Encrypt bytes using substitution table.
 */
export function encrypt(data: Uint8Array): Uint8Array {
    const result = new Uint8Array(data.length);
    for (let i = 0; i < data.length; i++) {
        result[i] = ENCRYPT_TABLE[data[i]];
    }
    return result;
}

/**
 * Decrypt bytes using substitution table.
 */
export function decrypt(data: Uint8Array): Uint8Array {
    const result = new Uint8Array(data.length);
    for (let i = 0; i < data.length; i++) {
        result[i] = DECRYPT_TABLE[data[i]];
    }
    return result;
}

/**
 * Encrypt string to base64 encoded string.
 */
export function encryptBase64(plain: string): string {
    if (!plain) return '';
    const bytes = new TextEncoder().encode(plain);
    const encrypted = encrypt(bytes);
    return bytesToBase64(encrypted);
}

/**
 * Decrypt base64 encoded string to plain string.
 */
export function decryptBase64(encrypted: string): string {
    if (!encrypted) return '';
    const bytes = base64ToBytes(encrypted);
    const decrypted = decrypt(bytes);
    return new TextDecoder().decode(decrypted);
}

// Base64 helpers (browser + node compatible)
function bytesToBase64(bytes: Uint8Array): string {
    if (typeof Buffer !== 'undefined') {
        return Buffer.from(bytes).toString('base64');
    }
    // Browser
    let binary = '';
    for (let i = 0; i < bytes.length; i++) {
        binary += String.fromCharCode(bytes[i]);
    }
    return btoa(binary);
}

function base64ToBytes(base64: string): Uint8Array {
    if (typeof Buffer !== 'undefined') {
        return new Uint8Array(Buffer.from(base64, 'base64'));
    }
    // Browser
    const binary = atob(base64);
    const bytes = new Uint8Array(binary.length);
    for (let i = 0; i < binary.length; i++) {
        bytes[i] = binary.charCodeAt(i);
    }
    return bytes;
}

// Random bytes helper
function randomBytes(count: number): Uint8Array {
    const bytes = new Uint8Array(count);
    if (typeof globalThis !== 'undefined' && globalThis.crypto?.getRandomValues) {
        globalThis.crypto.getRandomValues(bytes);
    } else {
        // Fallback (masking only)
        for (let i = 0; i < count; i++) {
            bytes[i] = Math.floor(Math.random() * 256);
        }
    }
    return bytes;
}

// ============================================================================
// CInt
// ============================================================================

/**
 * Lightweight masked integer (masking only, not a security feature).
 * State is fully represented by (save1, save2) - serialization safe.
 */
export class CInt {
    save1: number = 0;
    save2: number = 0;

    constructor(value?: number) {
        if (value !== undefined) {
            this.setValue(value);
        }
    }

    /**
     * Get the actual integer value using permutation decoding.
     * Permutation: value_b0 = s1_b0 ^ s2_b0, value_b1 = s1_b2 ^ s2_b2,
     *              value_b2 = s1_b1 ^ s2_b1, value_b3 = s1_b3 ^ s2_b3
     */
    getValue(): number {
        const s1_b0 = this.save1 & 0xFF;
        const s1_b1 = (this.save1 >>> 8) & 0xFF;
        const s1_b2 = (this.save1 >>> 16) & 0xFF;
        const s1_b3 = (this.save1 >>> 24) & 0xFF;

        const s2_b0 = this.save2 & 0xFF;
        const s2_b1 = (this.save2 >>> 8) & 0xFF;
        const s2_b2 = (this.save2 >>> 16) & 0xFF;
        const s2_b3 = (this.save2 >>> 24) & 0xFF;

        const v_b0 = s1_b0 ^ s2_b0;
        const v_b1 = s1_b2 ^ s2_b2;
        const v_b2 = s1_b1 ^ s2_b1;
        const v_b3 = s1_b3 ^ s2_b3;

        // Reconstruct as signed 32-bit integer
        const result = v_b0 | (v_b1 << 8) | (v_b2 << 16) | (v_b3 << 24);
        return result | 0; // Force signed 32-bit
    }

    /**
     * Set the integer value using permutation encoding.
     */
    setValue(value: number): void {
        value = value | 0; // Force signed 32-bit

        const v_b0 = value & 0xFF;
        const v_b1 = (value >>> 8) & 0xFF;
        const v_b2 = (value >>> 16) & 0xFF;
        const v_b3 = (value >>> 24) & 0xFF;

        // Generate random mask bytes
        const mask = randomBytes(4);
        const s2_b0 = mask[0];
        const s2_b1 = mask[1];
        const s2_b2 = mask[2];
        const s2_b3 = mask[3];

        // Inverse permutation
        const s1_b0 = v_b0 ^ s2_b0;
        const s1_b1 = v_b2 ^ s2_b1;
        const s1_b2 = v_b1 ^ s2_b2;
        const s1_b3 = v_b3 ^ s2_b3;

        this.save1 = (s1_b0 | (s1_b1 << 8) | (s1_b2 << 16) | (s1_b3 << 24)) | 0;
        this.save2 = (s2_b0 | (s2_b1 << 8) | (s2_b2 << 16) | (s2_b3 << 24)) | 0;
    }

    /**
     * Set raw save1/save2 values directly (for deserialization).
     */
    setRaw(s1: number, s2: number): void {
        this.save1 = s1 | 0;
        this.save2 = s2 | 0;
    }

    toString(): string {
        return this.getValue().toString();
    }
}

// ============================================================================
// CFloat
// ============================================================================

// Float <-> Int bits conversion using DataView (little-endian)
const floatBuffer = new ArrayBuffer(4);
const floatView = new DataView(floatBuffer);

function floatToInt32Bits(value: number): number {
    floatView.setFloat32(0, value, true); // little-endian
    return floatView.getInt32(0, true);
}

function int32BitsToFloat(bits: number): number {
    floatView.setInt32(0, bits, true); // little-endian
    return floatView.getFloat32(0, true);
}

/**
 * Lightweight masked float (masking only, not a security feature).
 * State is fully represented by (save1, save2) - serialization safe.
 */
export class CFloat {
    save1: number = 0;
    save2: number = 0;

    constructor(value?: number) {
        if (value !== undefined) {
            this.setValue(value);
        }
    }

    /**
     * Get the actual float value using permutation decoding.
     */
    getValue(): number {
        const s1_b0 = this.save1 & 0xFF;
        const s1_b1 = (this.save1 >>> 8) & 0xFF;
        const s1_b2 = (this.save1 >>> 16) & 0xFF;
        const s1_b3 = (this.save1 >>> 24) & 0xFF;

        const s2_b0 = this.save2 & 0xFF;
        const s2_b1 = (this.save2 >>> 8) & 0xFF;
        const s2_b2 = (this.save2 >>> 16) & 0xFF;
        const s2_b3 = (this.save2 >>> 24) & 0xFF;

        const v_b0 = s1_b0 ^ s2_b0;
        const v_b1 = s1_b2 ^ s2_b2;
        const v_b2 = s1_b1 ^ s2_b1;
        const v_b3 = s1_b3 ^ s2_b3;

        const bits = (v_b0 | (v_b1 << 8) | (v_b2 << 16) | (v_b3 << 24)) | 0;
        return int32BitsToFloat(bits);
    }

    /**
     * Set the float value using permutation encoding.
     */
    setValue(value: number): void {
        const bits = floatToInt32Bits(value);

        const v_b0 = bits & 0xFF;
        const v_b1 = (bits >>> 8) & 0xFF;
        const v_b2 = (bits >>> 16) & 0xFF;
        const v_b3 = (bits >>> 24) & 0xFF;

        // Generate random mask bytes
        const mask = randomBytes(4);
        const s2_b0 = mask[0];
        const s2_b1 = mask[1];
        const s2_b2 = mask[2];
        const s2_b3 = mask[3];

        // Inverse permutation
        const s1_b0 = v_b0 ^ s2_b0;
        const s1_b1 = v_b2 ^ s2_b1;
        const s1_b2 = v_b1 ^ s2_b2;
        const s1_b3 = v_b3 ^ s2_b3;

        this.save1 = (s1_b0 | (s1_b1 << 8) | (s1_b2 << 16) | (s1_b3 << 24)) | 0;
        this.save2 = (s2_b0 | (s2_b1 << 8) | (s2_b2 << 16) | (s2_b3 << 24)) | 0;
    }

    /**
     * Set raw save1/save2 values directly (for deserialization).
     */
    setRaw(s1: number, s2: number): void {
        this.save1 = s1 | 0;
        this.save2 = s2 | 0;
    }

    toString(): string {
        return this.getValue().toString();
    }
}

// ============================================================================
// CString
// ============================================================================

/**
 * Lightweight masked string (masking only, not a security feature).
 * State is fully represented by data (base64 encoded) - serialization safe.
 */
export class CString {
    data: string = '';

    constructor(plainValue?: string) {
        if (plainValue !== undefined) {
            this.setValue(plainValue);
        }
    }

    /**
     * Get the decrypted plain text value.
     * Returns empty string on failure.
     */
    getValue(): string {
        if (!this.data) return '';
        try {
            return decryptBase64(this.data);
        } catch (e) {
            error('Complex', 'CString.getValue failed to decrypt', e instanceof Error ? e : undefined);
            return '';
        }
    }

    /**
     * Set the value by encrypting the plain text.
     */
    setValue(plainValue: string): void {
        if (!plainValue) {
            this.data = '';
            return;
        }
        this.data = encryptBase64(plainValue);
    }

    /**
     * Set raw data directly (for deserialization).
     */
    setRaw(encryptedData: string): void {
        this.data = encryptedData || '';
    }

    toString(): string {
        return this.getValue();
    }
}

// ============================================================================
// Factory functions (convenience)
// ============================================================================

export function cInt(value: number): CInt {
    return new CInt(value);
}

export function cFloat(value: number): CFloat {
    return new CFloat(value);
}

export function cString(value: string): CString {
    return new CString(value);
}
