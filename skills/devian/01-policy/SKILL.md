# Devian — Framework Common Policy

## Scope

이 문서는 Devian 프레임워크 공통 운용 정책을 다룬다:
- 문서 구조 / SKILL 작성 규격
- 인덱스 원칙
- 플레이스홀더 사용 원칙
- SSOT 우선순위
- Git 정책

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

## SKILL 작성 규격

### SKILL 문서가 포함해야 하는 것

1) **정책(Policy)** — 무엇이 정본인지(SSOT), 금지/필수 규칙
2) **입력 규약(Input contract)** — 입력 파일 포맷, 테이블 작성 규칙
3) **경로 규약(Path contract)** — `{buildInputJson}` 기준 staging/final 경로 해석, 플레이스홀더 표준
4) **검증 규칙(Validation)** — 빌드 실패 조건(FAIL fast), Hard/Soft conflict 분류

### SKILL 문서에 쓰면 안 되는 것

아래는 **SKILL에서 금지**한다(코드가 정본):

- 생성되는 클래스/인터페이스/함수 시그니처
- 프레임 바이너리 레이아웃의 바이트 단위 정의
- 런타임 패키지의 파일 목록/구현 디테일
- "예시 코드"가 실제 코드 API와 동기화되지 않을 가능성이 있는 내용

예외: 입력 규약을 설명하는 최소한의 짧은 예시(JSON 한 조각, 테이블 한 셀 값 등)은 허용한다.

### Hard Conflicts / Soft Conflicts 표기 규칙

- Hard Conflicts: 빌드/호환성/입력 규약이 깨지는 수준 → **즉시 FAIL**
- Soft Conflicts: 용어/표기/톤/링크 → **정리 대상(충돌 아님)**

Soft를 이유로 "충돌 0개 만들기" 무한 루프를 돌리지 않는다.

### 문서 헤더 표준

모든 SKILL은 상단에 아래 메타를 가진다:

- `Status: ACTIVE | DRAFT`
- `AppliesTo: v10`
- `SSOT: skills/devian/10-module/03-ssot/SKILL.md`

---

## Git Policy

### 줄바꿈 문자 (Line Endings) — Hard Rule

| 파일 유형 | 줄바꿈 | 설명 |
|----------|--------|------|
| Shell 스크립트 (`*.sh`) | **LF** | CRLF 사용시 shebang 오류 발생 |
| 모든 텍스트 파일 | **LF** | 플랫폼 간 일관성 유지 |

`.gitattributes` 필수:

```gitattributes
* text=auto eol=lf
*.sh text eol=lf
*.png binary
*.jpg binary
*.dll binary
*.exe binary
```

CRLF 진단: `file script.sh` → "with CRLF line terminators" 표시 시 `sed -i 's/\r$//' script.sh`로 변환.

### 커밋 메시지 규칙

```
<type>: <subject>
```

| Type | 설명 |
|------|------|
| `feat` | 새 기능 |
| `fix` | 버그 수정 |
| `refactor` | 리팩토링 (기능 변경 없음) |
| `docs` | 문서 변경 |
| `chore` | 빌드, 설정 변경 |
| `test` | 테스트 추가/수정 |

### 브랜치 규칙

| 유형 | 패턴 | 예시 |
|------|------|------|
| 기능 | `feature/<name>` | `feature/ws-reconnect` |
| 버그 수정 | `fix/<name>` | `fix/login-crash` |
| 릴리스 | `release/<version>` | `release/10.1.0` |

보호 브랜치: `main`, `develop` — 직접 푸시 금지, PR 통해서만 병합.

### .gitignore 필수 항목

```gitignore
node_modules/
*.g.cs
*.g.ts
Generated/
.idea/
.vs/
*.suo
*.user
.DS_Store
Thumbs.db
*.env
*.local
```

### Git Hard Conflicts (DoD)

아래 상태가 발견되면 **FAIL**:

1. `.sh` 파일에 CRLF 줄바꿈 사용
2. `.gitattributes` 파일 없음
3. `node_modules/` 디렉토리가 커밋됨
4. `*.env` 파일이 커밋됨

---

## See Also

- [SSOT](../10-module/03-ssot/SKILL.md)
- [Devian Index](./SKILL.md)
- [Glossary](../04-glossary/SKILL.md)
