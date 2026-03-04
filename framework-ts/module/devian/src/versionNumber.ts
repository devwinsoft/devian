// SSOT: skills/devian/10-module/20-core/14-version-number/SKILL.md

// ============================================================================
// VersionNumber Type (#.#.# — Major.Minor.Patch)
// ============================================================================

export interface VersionNumber {
    readonly major: number;
    readonly minor: number;
    readonly patch: number;
}

// ============================================================================
// Construction
// ============================================================================

/**
 * Create a VersionNumber from major, minor, patch components.
 * All components must be non-negative integers.
 */
export function versionNumber(major: number, minor: number, patch: number): VersionNumber {
    if (!Number.isInteger(major) || major < 0)
        throw new Error(`major must be non-negative integer, got: ${major}`);
    if (!Number.isInteger(minor) || minor < 0)
        throw new Error(`minor must be non-negative integer, got: ${minor}`);
    if (!Number.isInteger(patch) || patch < 0)
        throw new Error(`patch must be non-negative integer, got: ${patch}`);
    return { major, minor, patch };
}

// ============================================================================
// Parse / TryParse
// ============================================================================

/**
 * Parse "#.#.#" format string into VersionNumber.
 * Throws on invalid input.
 */
export function parseVersionNumber(value: string): VersionNumber {
    const result = tryParseVersionNumber(value);
    if (result === null) {
        throw new Error(`Invalid version format: '${value}'. Expected '#.#.#'.`);
    }
    return result;
}

/**
 * Try parse "#.#.#" format string. Returns null on failure.
 */
export function tryParseVersionNumber(value: string): VersionNumber | null {
    if (!value) return null;

    const parts = value.split('.');
    if (parts.length !== 3) return null;

    const major = parseInt(parts[0], 10);
    const minor = parseInt(parts[1], 10);
    const patch = parseInt(parts[2], 10);

    if (isNaN(major) || major < 0) return null;
    if (isNaN(minor) || minor < 0) return null;
    if (isNaN(patch) || patch < 0) return null;

    return { major, minor, patch };
}

// ============================================================================
// Comparison
// ============================================================================

/**
 * Compare two VersionNumbers.
 * Returns negative if a < b, 0 if equal, positive if a > b.
 */
export function compareVersionNumber(a: VersionNumber, b: VersionNumber): number {
    if (a.major !== b.major) return a.major - b.major;
    if (a.minor !== b.minor) return a.minor - b.minor;
    return a.patch - b.patch;
}

/**
 * Check equality of two VersionNumbers.
 */
export function versionNumberEquals(a: VersionNumber, b: VersionNumber): boolean {
    return a.major === b.major && a.minor === b.minor && a.patch === b.patch;
}

// ============================================================================
// Display
// ============================================================================

/**
 * Format VersionNumber as "#.#.#" string.
 */
export function versionNumberToString(v: VersionNumber): string {
    return `${v.major}.${v.minor}.${v.patch}`;
}
