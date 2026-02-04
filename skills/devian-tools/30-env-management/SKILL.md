# 30-env-management — Environment Variable Management Policy

Status: ACTIVE
AppliesTo: v10
Type: POLICY

---

## Verification Target

- **검증 대상**: main branch (현재 커밋)
- **포함**: `framework-ts/apps/*`, `framework-ts/tools/env/*`
- **제외**: Unity build, WebGL runtime, framework-cs
- **관련 SSOT**:
  - `{appRoot}/env.spec.json` (환경변수 정본)
  - `framework-ts/tools/env/env-sync.mjs` (생성 도구)
  - `framework-ts/tools/env/env-check.mjs` (검증 도구)

---

## Scope

모든 framework-ts 앱의 환경변수 정의 및 `.env.example` 생성 정책

**포함**: WebGLServer, GameServer, 향후 추가되는 모든 framework-ts 앱
**제외**: Unity 프로젝트, C# 코드, 빌드 산출물

---

## SSOT

| 구분 | 경로 |
|------|------|
| **정본 (SSOT)** | `{appRoot}/env.spec.json` |
| **생성물** | `{appRoot}/.env.example` (자동 생성, 수동 편집 금지) |
| **공통 도구** | `framework-ts/tools/env/env-sync.mjs`, `env-check.mjs` |

---

## Rules (MUST/FAIL)

1. **SSOT 고정**: 각 앱의 환경변수 정의는 `{appRoot}/env.spec.json` 하나로 고정 — 분산 정의 FAIL
2. **생성물 보호**: `.env.example`는 수동 편집 금지, 오직 `env:sync`로만 생성/갱신 — 수동 수정 FAIL
3. **검증 필수**: `env:check`가 `.env.example`이 최신이 아니면 `exit 1`로 실패 — 검증 없이 완료 선언 FAIL
4. **.env 커밋 금지**: `.env` 파일은 절대 커밋하지 않음 — 커밋 시 FAIL
5. **코드 동기화**: 코드에서 env 키 추가 시 반드시 `env.spec.json`에도 추가 — 누락 FAIL

---

## env.spec.json Schema

```json
[
  {
    "key": "ENV_KEY",
    "default": "value",
    "description": "Description of the variable",
    "required": false
  }
]
```

| 필드 | 타입 | 필수 | 설명 |
|------|------|------|------|
| `key` | string | O | 환경변수 이름 |
| `default` | string \| number \| boolean | O | 기본값 |
| `description` | string | O | 설명 |
| `required` | boolean | X | 필수 여부 (기본: false) |

---

## Workflow (고정 절차)

환경변수 추가/변경 시 반드시 아래 순서를 따른다:

```bash
# 1. env.spec.json 수정 (에디터에서)

# 2. .env.example 생성/갱신
npm -w <workspace> run env:sync

# 3. 검증
npm -w <workspace> run env:check

# 4. 커밋 (env.spec.json + .env.example 함께)
git add apps/<app>/env.spec.json apps/<app>/.env.example
git commit -m "env: add/update <KEY> for <app>"
```

---

## DoD (Hard=0 체크리스트)

- [ ] `env.spec.json`이 정본으로 존재
- [ ] `.env.example`이 `env:sync`로 생성됨
- [ ] `npm -w <workspace> run env:check` → exit 0
- [ ] `package.json`에 `env:sync`, `env:check` 스크립트 존재
- [ ] `prestart` 또는 `prestart:dev`에서 `env:check` 호출
- [ ] 코드에서 사용하는 env 키가 모두 `env.spec.json`에 정의됨

---

## Anti-patterns (금지사항)

1. **`.env.example` 수동 수정** — regen 시 되돌아오는 패치, 근본 해결 없음
2. **`.env` 파일 커밋** — 비밀 유출 위험
3. **코드에서 env 키 추가 후 `env.spec.json` 누락** — 문서/코드 불일치
4. **앱별 개별 env 스크립트 생성** — 공통 tool로 통일 필수
5. **`env:check` 없이 "완료" 선언** — 검증 누락
6. **문서 경로와 실제 경로 불일치** — 복붙 시 흔한 실수

---

## Commands

```bash
# .env.example 생성/갱신
npm -w <workspace> run env:sync

# .env.example 최신 여부 검증
npm -w <workspace> run env:check

# 전체 앱 검증 (예시)
npm -w webgl-server run env:check && npm -w game-server run env:check
```

---

## Change Log

| 날짜 | 변경 | 영향 범위 |
|------|------|----------|
| 2026-02-04 | 초기 정책 생성 | WebGLServer, GameServer |

---

## Related

- [03-ssot](../03-ssot/SKILL.md) — Tools SSOT
- [32-example-webgl-server](../../devian-examples/32-example-webgl-server/SKILL.md) — WebGL 서버 예제
