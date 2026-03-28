# devian/20-domain-common — Overview

Devian Common Domain의 C#/TS 공통 정책과 결과 표현 규약을 담당한다.

- **UPM 위치**: `com.devian.foundation/Samples~/CommonPackage/`
- **Assembly**: `Devian.Samples.CommonPackage`
- **Domain Policy**: `Common` DomainKey의 역할/제약
- **Module Policy**: Common 모듈의 생성물/수동 코드 경계, 의존성, namespace 규칙
- **Error/Result**: `CommonError`, `CommonResult<T>`, `COMMON_ERROR` 규약

---

## Boundary

이 그룹은 **부모(기반) 스킬 그룹**이며, 소비자 그룹의 이름/링크를 포함하지 않는다.

## Start Here

| Document | Description |
|----------|-------------|
| [01-policy](../01-policy/SKILL.md) | Common Domain 정책 |
| [02-module-policy](../02-module-policy/SKILL.md) | Common 모듈 정책 (C#/TS 공통) |
| [16-common-error](../16-common-error/SKILL.md) | CommonError / COMMON_ERROR_TYPE / COMMON_ERROR |
| [17-common-result](../17-common-result/SKILL.md) | CommonResult 규약 |
| [20-common-utils](../20-common-utils/SKILL.md) | CommonUtils (언어 코드 변환 등 공용 헬퍼) |

---

## Related

- [Root SSOT](../../10-module/03-ssot/SKILL.md)
- [Core Features](../../10-module/20-core/00-overview/SKILL.md)
- [Devian Index](../../SKILL.md)
