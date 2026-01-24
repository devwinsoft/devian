# Devian v10 — Common Feature: Crypto

Status: DRAFT  
AppliesTo: v10  
SSOT: skills/devian/03-ssot/SKILL.md

## Overview

Common 모듈의 암호화/복호화 기능을 정의한다.

이 기능은 프로젝트 전역에서 사용할 수 있는 표준 암호화 유틸리티를 제공한다.

---

## Responsibilities

- AES 대칭 암호화/복호화
- 안전한 Key/IV 생성
- Base64 인코딩된 암호문 반환

---

## Non-goals

- 비대칭 암호화 (RSA 등) — 필요 시 별도 feature로 분리
- 키 관리/저장 — 애플리케이션 책임
- TLS/SSL 통신 — 네트워크 레이어 책임
- 자체 암호화 알고리즘 구현 — **금지**

---

## Hard Rules (MUST)

1. **자체 암호화 알고리즘 구현 금지** — 플랫폼/라이브러리 제공 구현만 사용
2. **안전한 알고리즘만 사용** — AES-256, SHA-256 이상
3. **키/IV 하드코딩 금지** — 런타임에 주입받거나 안전하게 생성
4. **평문 키 로깅 금지**

---

## Public API

### C#

```csharp
namespace Devian
{
    public static class Crypto
    {
        // AES-256 암호화 (Base64 반환)
        public static string EncryptAes(string plainText, byte[] key, byte[] iv);
        
        // AES-256 복호화
        public static string DecryptAes(string cipherTextBase64, byte[] key, byte[] iv);
        
        // 안전한 Key 생성 (256-bit)
        public static byte[] GenerateKey();
        
        // 안전한 IV 생성 (128-bit)
        public static byte[] GenerateIv();
    }
}
```

### TypeScript (`devian-module-common/features`)

```typescript
// features/crypto.ts

/**
 * AES-256 암호화 (Base64 반환)
 */
export function encryptAes(plainText: string, key: Uint8Array, iv: Uint8Array): string;

/**
 * AES-256 복호화
 */
export function decryptAes(cipherTextBase64: string, key: Uint8Array, iv: Uint8Array): string;

/**
 * 안전한 Key 생성 (256-bit)
 */
export function generateKey(): Uint8Array;

/**
 * 안전한 IV 생성 (128-bit)
 */
export function generateIv(): Uint8Array;
```

---

## Dependency Rules

Common module policy를 따른다:

- Common 기능은 다른 생성 모듈을 참조하지 않는다.
- 외부 라이브러리 의존은 최소화한다.
- C#: `System.Security.Cryptography` 사용
- TS: Web Crypto API 또는 Node.js `crypto` 모듈 사용

---

## Examples

### C#

```csharp
using Devian;

// 키/IV 생성
var key = Crypto.GenerateKey();
var iv = Crypto.GenerateIv();

// 암호화
var encrypted = Crypto.EncryptAes("Hello, World!", key, iv);

// 복호화
var decrypted = Crypto.DecryptAes(encrypted, key, iv);
// decrypted == "Hello, World!"
```

### TypeScript

```typescript
import { encryptAes, decryptAes, generateKey, generateIv } from '@devian/module-common/features';

// 키/IV 생성
const key = generateKey();
const iv = generateIv();

// 암호화
const encrypted = encryptAes('Hello, World!', key, iv);

// 복호화
const decrypted = decryptAes(encrypted, key, iv);
// decrypted === 'Hello, World!'
```

---

## DoD (Definition of Done)

- [ ] C# `Crypto` static class 구현
- [ ] TS `crypto.ts` 모듈 구현
- [ ] 단위 테스트 작성 (암호화/복호화 왕복)
- [ ] 키/IV 생성 테스트
- [ ] 잘못된 키/IV로 복호화 시 예외 처리 확인
- [ ] features/index.ts에 자동 export 확인

---

## Reference

- Module Policy: `skills/devian-common-feature/01-module-policy/SKILL.md`
- Domain Policy: `skills/devian-common-feature/00-domain-policy/SKILL.md`
