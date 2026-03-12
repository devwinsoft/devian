/**
 * DVN Codec — .dvn file encode/decode pipeline.
 * v2: HMAC-SHA256 integrity verification included.
 *
 * SSOT: skills/devian-unity/50-mobile-system/25-recovery-system/03-ssot/SKILL.md
 */
import { obfuscate, deobfuscate } from './obfuscation-codec';
const DVN_VERSION_V1 = 0x01;
const DVN_VERSION_V2 = 0x02;
const HMAC_SEPARATOR = '\n';
const HMAC_HEX_LENGTH = 64; // SHA-256 → 32 bytes → 64 hex chars
// APP_SECRET: C#/TS 양쪽 동일 값. §E HMAC Integrity 참조.
const APP_SECRET = 'dvn-k8x2pQ7mW4rJ9sN1vF6bY3hT0cL5';
/**
 * Encode JSON string to .dvn file content (v2: HMAC included).
 * Pipeline: JSON → HMAC sign → ComplexUtil.Encrypt_Base64 → version prefix
 */
export async function encodeDvn(json) {
    const socialUserId = extractSocialUserId(json);
    const hmacHex = await computeHmac(json, socialUserId);
    const signedPayload = json + HMAC_SEPARATOR + hmacHex;
    const obfuscated = obfuscate(signedPayload);
    return String.fromCharCode(DVN_VERSION_V2) + obfuscated;
}
/**
 * Decode .dvn file content to JSON string.
 * v2: HMAC integrity verification included.
 * v1: backward compatible (no HMAC).
 */
export async function decodeDvn(dvnContent) {
    if (dvnContent.length < 2) {
        throw new Error('Invalid .dvn file: too short');
    }
    const version = dvnContent.charCodeAt(0);
    switch (version) {
        case DVN_VERSION_V1:
            return decodeDvnV1(dvnContent);
        case DVN_VERSION_V2:
            return decodeDvnV2(dvnContent);
        default:
            throw new Error(`Unsupported .dvn version: ${version}`);
    }
}
// ──────────────────────────────────────
// v1 backward compat: no HMAC
// ──────────────────────────────────────
function decodeDvnV1(dvnContent) {
    const payload = dvnContent.substring(1);
    return deobfuscate(payload);
}
// ──────────────────────────────────────
// v2: HMAC verification
// ──────────────────────────────────────
async function decodeDvnV2(dvnContent) {
    const obfuscated = dvnContent.substring(1);
    const signedPayload = deobfuscate(obfuscated);
    // signedPayload = json + "\n" + hmac_hex_64
    if (signedPayload.length < HMAC_HEX_LENGTH + HMAC_SEPARATOR.length + 1) {
        throw new Error('DVN v2 signed payload too short');
    }
    const separatorIndex = signedPayload.length - HMAC_HEX_LENGTH - HMAC_SEPARATOR.length;
    const separator = signedPayload.substring(separatorIndex, separatorIndex + HMAC_SEPARATOR.length);
    if (separator !== HMAC_SEPARATOR) {
        throw new Error('DVN v2 HMAC separator not found');
    }
    const json = signedPayload.substring(0, separatorIndex);
    const hmacHex = signedPayload.substring(separatorIndex + HMAC_SEPARATOR.length);
    // HMAC verification
    const socialUserId = extractSocialUserId(json);
    const expectedHmac = await computeHmac(json, socialUserId);
    if (hmacHex !== expectedHmac) {
        throw new Error('DVN v2 HMAC verification failed');
    }
    return json;
}
// ──────────────────────────────────────
// Private helpers
// ──────────────────────────────────────
async function computeHmac(json, socialUserId) {
    const keyString = APP_SECRET + (socialUserId ?? '');
    const encoder = new TextEncoder();
    const keyBytes = encoder.encode(keyString);
    const dataBytes = encoder.encode(json);
    const cryptoKey = await crypto.subtle.importKey('raw', keyBytes, { name: 'HMAC', hash: 'SHA-256' }, false, ['sign']);
    const signature = await crypto.subtle.sign('HMAC', cryptoKey, dataBytes);
    const hashArray = Array.from(new Uint8Array(signature));
    return hashArray.map((b) => b.toString(16).padStart(2, '0')).join('');
}
function extractSocialUserId(json) {
    try {
        const obj = JSON.parse(json);
        return obj?.account?.socialUserId ?? null;
    }
    catch {
        return null;
    }
}
