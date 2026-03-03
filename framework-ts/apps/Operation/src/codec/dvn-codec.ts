/**
 * DVN Codec — .dvn file encode/decode pipeline.
 *
 * Encode: JSON → ComplexUtil.Encrypt_Base64 → version prefix → .dvn
 * Decode: .dvn → version parse → ComplexUtil.Decrypt_Base64 → JSON
 *
 * SSOT: skills/devian-unity/50-mobile-system/25-recovery-system/03-ssot/SKILL.md
 */

import { obfuscate, deobfuscate } from './obfuscation-codec';

const DVN_VERSION = 0x01;

/**
 * Encode JSON string to .dvn file content.
 * Pipeline: JSON → ComplexUtil.Encrypt_Base64 → version prefix
 */
export function encodeDvn(json: string): string {
  const obfuscated = obfuscate(json);
  return String.fromCharCode(DVN_VERSION) + obfuscated;
}

/**
 * Decode .dvn file content to JSON string.
 * Pipeline: version parse → ComplexUtil.Decrypt_Base64 → JSON
 */
export function decodeDvn(dvnContent: string): string {
  if (dvnContent.length < 2) {
    throw new Error('Invalid .dvn file: too short');
  }

  const version = dvnContent.charCodeAt(0);
  if (version !== DVN_VERSION) {
    throw new Error(`Unsupported .dvn version: ${version} (expected ${DVN_VERSION})`);
  }

  const payload = dvnContent.substring(1);
  return deobfuscate(payload);
}
