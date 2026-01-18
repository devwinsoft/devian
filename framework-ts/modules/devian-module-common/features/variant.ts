// SSOT: skills/devian-common/11-feature-variant/SKILL.md

import { CInt, CFloat, encryptBase64, decryptBase64 } from './complex';

// ============================================================================
// Shape Interfaces (for JSON serialization)
// ============================================================================

export interface CIntShape {
    save1: number;
    save2: number;
}

export interface CFloatShape {
    save1: number;
    save2: number;
}

export interface CStringShape {
    data: string;
}

// ============================================================================
// Variant Type (Tagged Union + Complex shape)
// ============================================================================

/**
 * Variant tagged union type using Complex shapes.
 * JSON representation: {"k":"i"|"f"|"s", "i"|"f"|"s": {shape}}
 */
export type Variant =
    | { k: 'i'; i: CIntShape }
    | { k: 'f'; f: CFloatShape }
    | { k: 's'; s: CStringShape };

// ============================================================================
// Factory Functions (from plain values)
// ============================================================================

/**
 * Create Variant from integer value.
 * Uses CInt internally to generate save1/save2.
 */
export function vInt(value: number): Variant {
    const ci = new CInt(Math.trunc(value));
    return { k: 'i', i: { save1: ci.save1, save2: ci.save2 } };
}

/**
 * Create Variant from float value.
 * Uses CFloat internally to generate save1/save2.
 */
export function vFloat(value: number): Variant {
    const cf = new CFloat(value);
    return { k: 'f', f: { save1: cf.save1, save2: cf.save2 } };
}

/**
 * Create Variant from string value.
 * Uses encryptBase64 internally to generate masked data.
 */
export function vString(value: string): Variant {
    const data = encryptBase64(value);
    return { k: 's', s: { data } };
}

// ============================================================================
// Raw Factory Functions (for deserialization)
// ============================================================================

/**
 * Create Variant from raw CInt shape (for deserialization).
 */
export function vIntRaw(save1: number, save2: number): Variant {
    return { k: 'i', i: { save1, save2 } };
}

/**
 * Create Variant from raw CFloat shape (for deserialization).
 */
export function vFloatRaw(save1: number, save2: number): Variant {
    return { k: 'f', f: { save1, save2 } };
}

/**
 * Create Variant from raw CString shape (for deserialization).
 */
export function vStringRaw(data: string): Variant {
    return { k: 's', s: { data } };
}

// ============================================================================
// Type Guards
// ============================================================================

export function isInt(v: Variant): v is { k: 'i'; i: CIntShape } {
    return v.k === 'i';
}

export function isFloat(v: Variant): v is { k: 'f'; f: CFloatShape } {
    return v.k === 'f';
}

export function isString(v: Variant): v is { k: 's'; s: CStringShape } {
    return v.k === 's';
}

// ============================================================================
// Accessors (decode from Complex shape)
// ============================================================================

/**
 * Get integer value from Variant.
 * Decodes from CInt shape using permutation.
 */
export function asInt(v: Variant): number {
    if (v.k !== 'i') throw new Error(`Variant is ${v.k}, not i`);
    const ci = new CInt();
    ci.setRaw(v.i.save1, v.i.save2);
    return ci.getValue();
}

/**
 * Get float value from Variant.
 * Decodes from CFloat shape using permutation.
 */
export function asFloat(v: Variant): number {
    if (v.k !== 'f') throw new Error(`Variant is ${v.k}, not f`);
    const cf = new CFloat();
    cf.setRaw(v.f.save1, v.f.save2);
    return cf.getValue();
}

/**
 * Get string value from Variant.
 * Decodes from CString shape using decryptBase64.
 */
export function asString(v: Variant): string {
    if (v.k !== 's') throw new Error(`Variant is ${v.k}, not s`);
    return decryptBase64(v.s.data);
}

// ============================================================================
// Parsing (Table Input Format)
// ============================================================================

/**
 * Parse table input format: "i:123", "f:3.5", "s:Hello"
 * Returns Variant with Complex shape.
 */
export function parseVariant(input: string): Variant {
    if (!input || input.length < 2) {
        throw new Error(`Invalid Variant format: '${input}'`);
    }

    const trimmed = input.trim();
    if (trimmed.length < 2 || trimmed[1] !== ':') {
        throw new Error(`Invalid Variant format: '${input}'. Expected 'i:', 'f:', or 's:' prefix.`);
    }

    const prefix = trimmed[0];
    const body = trimmed.substring(2);

    switch (prefix) {
        case 'i': {
            const value = parseInt(body, 10);
            if (isNaN(value)) {
                throw new Error(`Invalid integer value in Variant: '${input}'`);
            }
            return vInt(value);
        }
        case 'f': {
            const value = parseFloat(body);
            if (isNaN(value)) {
                throw new Error(`Invalid float value in Variant: '${input}'`);
            }
            return vFloat(value);
        }
        case 's':
            return vString(body);
        default:
            throw new Error(`Invalid Variant prefix '${prefix}' in '${input}'. Expected 'i', 'f', or 's'.`);
    }
}

/**
 * Try parse, returns null on failure
 */
export function tryParseVariant(input: string): Variant | null {
    try {
        return parseVariant(input);
    } catch {
        return null;
    }
}

// ============================================================================
// Serialization Helpers
// ============================================================================

/**
 * Convert Variant to table input format string (decoded value).
 */
export function variantToString(v: Variant): string {
    switch (v.k) {
        case 'i': return `i:${asInt(v)}`;
        case 'f': return `f:${asFloat(v)}`;
        case 's': return `s:${asString(v)}`;
    }
}

/**
 * Compare two variants for equality (by decoded values).
 */
export function variantEquals(a: Variant, b: Variant): boolean {
    if (a.k !== b.k) return false;
    switch (a.k) {
        case 'i': return asInt(a) === asInt(b as { k: 'i'; i: CIntShape });
        case 'f': return asFloat(a) === asFloat(b as { k: 'f'; f: CFloatShape });
        case 's': return asString(a) === asString(b as { k: 's'; s: CStringShape });
    }
}
