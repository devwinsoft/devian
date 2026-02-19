# Devian — Framework Common Policy

## Scope

이 문서는 Devian 프레임워크 공통 운용 정책을 다룬다:
- 문서 구조
- 인덱스 원칙
- 플레이스홀더 사용 원칙
- SSOT 우선순위

---

## SSOT 우선순위

- **정본**: [skills/devian/10-module/03-ssot/SKILL.md](../10-module/03-ssot/SKILL.md)
- 충돌 시 SSOT 문서가 우선한다.
- 다른 문서에서 SSOT 상세 규칙을 장문 복제하지 않는다 (링크로 해결).

---

## Skills 구조 규칙

### 필수 폴더 구조

모든 `skills/devian-*` 폴더는 다음을 필수로 가진다:

| 폴더 | 역할 |
|------|------|
| `00-overview/` | 그룹 개요, 핵심 링크 |
| `01-policy/` | 그룹 정책/규약 |

### Index 문서

- `skills/devian/SKILL.md`는 전체 인덱스(목차) 역할을 한다.
- 각 그룹의 `00-overview`는 그룹 내 문서 탐색 시작점이다.

---

## Input JSON 플레이스홀더 원칙

### 표준 플레이스홀더

| Placeholder | 설명 | 예시 |
|-------------|------|------|
| `{buildInputJson}` | 빌드 입력 JSON 경로 | `input/input_common.json` |
| `{projectConfigJson}` | 프로젝트 설정 JSON 경로 | `input/config.json` |

### 사용 규칙

1. 문서에서 특정 파일명을 하드코딩하지 않는다.
2. 플레이스홀더를 사용하고, 예시는 괄호로 표기한다.
   - Good: `{buildInputJson}` (예: `input/input_common.json`)
   - Bad: `input/input_common.json`

---

## Link Policy

- 상세 규칙(금지 키, 머지, 경로 템플릿)은 [SSOT](../10-module/03-ssot/SKILL.md)로 링크한다.
- 중복 서술을 금지한다.
- 링크는 상대 경로를 사용한다.

---

## 00-overview 규칙

각 그룹의 `00-overview/SKILL.md`는 다음을 포함한다:

1. **그룹 설명** (3~6줄): 이 그룹이 담당하는 것
2. **Start Here 표**:
   - 해당 그룹 `01-policy`
   - 그룹에서 가장 중요한 2~4개 문서 링크
3. **Related**:
   - SSOT 링크
   - Devian Index 링크

---

## See Also

- [SSOT](../10-module/03-ssot/SKILL.md)
- [Devian Index](./SKILL.md)
- [Glossary](../04-glossary/SKILL.md)
