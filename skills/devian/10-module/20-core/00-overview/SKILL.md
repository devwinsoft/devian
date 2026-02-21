# devian/10-module/20-core — Overview

Devian Core layer: 도메인/프로토콜 생성물이 공통으로 의존하는 빌드/공용 유틸리티.

Maps to: `framework-cs/module/Devian/src/Core/`, `framework-cs/module/Devian/src/Variable/`

- **Crypto**: AES 대칭 암호화/복호화
- **Logger**: 4-level 로깅 + 교체 가능한 Sink
- **Variable/Complex**: 마스킹 타입 (CInt, CFloat, CString)
- **Variable/Variant**: 태그 유니온 (Int/Float/String)
- **Variable/BigInt**: 과학적 표기 큰 정수 (CBigInt)

---

## Start Here

| Document | Description |
|----------|-------------|
| [10-feature-crypto](../10-feature-crypto/SKILL.md) | Crypto Feature |
| [12-logger](../12-logger/SKILL.md) | Logger Feature |
| [31-variable-complex](../31-variable-complex/SKILL.md) | Complex (masking) types |
| [32-variable-variant](../32-variable-variant/SKILL.md) | Variant tagged union |
| [35-variable-bigint](../35-variable-bigint/SKILL.md) | CBigInt large number |

---

## Related

- [Parent Policy](../../01-policy/SKILL.md)
- [SSOT](../../03-ssot/SKILL.md)
- [Devian Index](../../../SKILL.md)
