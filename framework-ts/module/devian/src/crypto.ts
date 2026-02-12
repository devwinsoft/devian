/**
 * AES-256 (CBC) 암/복호화 유틸
 * - 반환/입력 cipherText는 Base64
 * - key: 32 bytes (256-bit)
 * - iv: 16 bytes (128-bit)
 */

function assertBytesLen(name: string, bytes: Uint8Array, expected: number) {
  if (bytes.length !== expected) {
    throw new Error(`${name} must be ${expected} bytes. Got: ${bytes.length}`);
  }
}

function toBase64(bytes: Uint8Array): string {
  // Node
  if (typeof Buffer !== 'undefined') return Buffer.from(bytes).toString('base64');
  // Browser
  let binary = '';
  for (let i = 0; i < bytes.length; i++) binary += String.fromCharCode(bytes[i]);
  return btoa(binary);
}

function fromBase64(b64: string): Uint8Array {
  // Node
  if (typeof Buffer !== 'undefined') return new Uint8Array(Buffer.from(b64, 'base64'));
  // Browser
  const bin = atob(b64);
  const out = new Uint8Array(bin.length);
  for (let i = 0; i < bin.length; i++) out[i] = bin.charCodeAt(i);
  return out;
}

async function getWebCrypto(): Promise<Crypto> {
  // Browser
  if (typeof globalThis !== 'undefined' && (globalThis as any).crypto?.subtle) return (globalThis as any).crypto as Crypto;

  // Node (>=19 usually has global crypto; fallback to node:crypto.webcrypto)
  try {
    const nodeCrypto = await import('node:crypto');
    if ((nodeCrypto as any).webcrypto?.subtle) return (nodeCrypto as any).webcrypto as Crypto;
  } catch {
    // ignore
  }

  throw new Error('WebCrypto is not available in this runtime.');
}

export async function encryptAes(plainText: string, key: Uint8Array, iv: Uint8Array): Promise<string> {
  assertBytesLen('key', key, 32);
  assertBytesLen('iv', iv, 16);

  const crypto = await getWebCrypto();
  const subtle = crypto.subtle;

  const cryptoKey = await subtle.importKey(
    'raw',
    key,
    { name: 'AES-CBC' },
    false,
    ['encrypt']
  );

  const data = new TextEncoder().encode(plainText);
  const encrypted = await subtle.encrypt({ name: 'AES-CBC', iv }, cryptoKey, data);
  return toBase64(new Uint8Array(encrypted));
}

export async function decryptAes(cipherTextBase64: string, key: Uint8Array, iv: Uint8Array): Promise<string> {
  assertBytesLen('key', key, 32);
  assertBytesLen('iv', iv, 16);

  const crypto = await getWebCrypto();
  const subtle = crypto.subtle;

  const cryptoKey = await subtle.importKey(
    'raw',
    key,
    { name: 'AES-CBC' },
    false,
    ['decrypt']
  );

  const cipherBytes = fromBase64(cipherTextBase64);
  const decrypted = await subtle.decrypt({ name: 'AES-CBC', iv }, cryptoKey, cipherBytes);
  return new TextDecoder().decode(new Uint8Array(decrypted));
}

export async function generateKey(): Promise<Uint8Array> {
  const crypto = await getWebCrypto();
  const key = new Uint8Array(32);
  crypto.getRandomValues(key);
  return key;
}

export async function generateIv(): Promise<Uint8Array> {
  const crypto = await getWebCrypto();
  const iv = new Uint8Array(16);
  crypto.getRandomValues(iv);
  return iv;
}
