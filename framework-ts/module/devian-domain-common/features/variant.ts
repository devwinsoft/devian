// SSOT: skills/devian-common/11-feature-variant/SKILL.md

// ============================================================================
// Variant Type (Simple format: {i} | {f} | {s})
// ============================================================================

/**
 * Variant tagged union type.
 * JSON representation: {"i": number} | {"f": number} | {"s": string}
 * Exactly one key per object.
 */
export type Variant =
    | { i: number }
    | { f: number }
    | { s: string };

// ============================================================================
// Factory Functions
// ============================================================================

/**
 * Create Variant from integer value.
 */
export function vInt(value: number): Variant {
    if (!Number.isInteger(value)) {
        throw new Error(`vInt requires integer, got: ${value}`);
    }
    return { i: value };
}

/**
 * Create Variant from float value.
 */
export function vFloat(value: number): Variant {
    return { f: value };
}

/**
 * Create Variant from string value.
 */
export function vString(value: string): Variant {
    return { s: value };
}

// ============================================================================
// Type Guards
// ============================================================================

export function isInt(v: Variant): v is { i: number } {
    return 'i' in v;
}

export function isFloat(v: Variant): v is { f: number } {
    return 'f' in v;
}

export function isString(v: Variant): v is { s: string } {
    return 's' in v;
}

// ============================================================================
// Accessors
// ============================================================================

/**
 * Get integer value from Variant.
 */
export function asInt(v: Variant): number {
    if (!isInt(v)) throw new Error(`Variant is not 'i' type`);
    return v.i;
}

/**
 * Get float value from Variant.
 */
export function asFloat(v: Variant): number {
    if (!isFloat(v)) throw new Error(`Variant is not 'f' type`);
    return v.f;
}

/**
 * Get string value from Variant.
 */
export function asString(v: Variant): string {
    if (!isString(v)) throw new Error(`Variant is not 's' type`);
    return v.s;
}

// ============================================================================
// Validation
// ============================================================================

/**
 * Validate Variant structure.
 * Must have exactly one key (i, f, or s) with correct value type.
 */
export function validateVariant(obj: unknown): Variant {
    if (obj === null || typeof obj !== 'object') {
        throw new Error('Variant must be an object');
    }
    
    const keys = Object.keys(obj);
    if (keys.length !== 1) {
        throw new Error(`Variant must have exactly one key (i, f, or s), got: ${keys.join(', ')}`);
    }
    
    const key = keys[0];
    const value = (obj as Record<string, unknown>)[key];
    
    switch (key) {
        case 'i':
            if (typeof value !== 'number' || !Number.isInteger(value)) {
                throw new Error(`Variant 'i' value must be integer, got: ${typeof value}`);
            }
            return { i: value };
        case 'f':
            if (typeof value !== 'number') {
                throw new Error(`Variant 'f' value must be number, got: ${typeof value}`);
            }
            return { f: value };
        case 's':
            if (typeof value !== 'string') {
                throw new Error(`Variant 's' value must be string, got: ${typeof value}`);
            }
            return { s: value };
        default:
            throw new Error(`Invalid Variant key '${key}'. Expected 'i', 'f', or 's'.`);
    }
}

// ============================================================================
// Parsing (Table Input Format)
// ============================================================================

/**
 * Parse table input format: "i:123", "f:3.5", "s:Hello"
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
            if (body.includes('.')) {
                throw new Error(`Integer value cannot have decimal: '${input}'`);
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
 * Convert Variant to table input format string.
 */
export function variantToString(v: Variant): string {
    if (isInt(v)) return `i:${v.i}`;
    if (isFloat(v)) return `f:${v.f}`;
    if (isString(v)) return `s:${v.s}`;
    throw new Error('Invalid Variant');
}

/**
 * Compare two variants for equality.
 */
export function variantEquals(a: Variant, b: Variant): boolean {
    if (isInt(a) && isInt(b)) return a.i === b.i;
    if (isFloat(a) && isFloat(b)) return a.f === b.f;
    if (isString(a) && isString(b)) return a.s === b.s;
    return false;
}

/**
 * Get the kind of variant: 'i', 'f', or 's'
 */
export function variantKind(v: Variant): 'i' | 'f' | 's' {
    if (isInt(v)) return 'i';
    if (isFloat(v)) return 'f';
    if (isString(v)) return 's';
    throw new Error('Invalid Variant');
}
