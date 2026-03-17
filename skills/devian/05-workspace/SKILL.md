# Devian v10 — Workspace

Status: ACTIVE
AppliesTo: v10
SSOT: skills/devian/10-module/03-ssot/SKILL.md

## Purpose

Devian TypeScript 프로젝트의 단일 workspace 구조와 npm 정책을 정의한다.

---

## Hard Rules (MUST)

### 1. 단일 Workspace Root

- **Root**: `framework-ts/package.json`만 workspace root
- **Lockfile**: `framework-ts/package-lock.json`만 유효
- **node_modules**: `framework-ts/node_modules`만 존재

### 2. 하위 폴더 Install 금지

하위 폴더(module/*, apps/*, tools/*)에서 직접 `npm install` 금지.
반드시 `framework-ts/`에서 실행한다.

### 3. 하위 node_modules 금지

하위 폴더에 `node_modules/`가 존재하면 정책 위반이다.

- Vite 등 빌드 도구가 기본값으로 로컬 `node_modules/.vite/`에 캐시를 생성할 수 있다.
- 반드시 `cacheDir`를 `framework-ts/node_modules/.vite/<app>`으로 지정하여 방지한다.
- 예: `apps/Operation/vite.config.ts` → `cacheDir: path.resolve(__dirname, '../../node_modules/.vite/operation')`

Recovery (하위 node_modules 발견 시):

```bash
# 1. 하위 node_modules 삭제
rm -rf framework-ts/apps/<app>/node_modules

# 2. 해당 앱의 vite.config.ts에서 cacheDir가 올바른지 확인
# cacheDir → framework-ts/node_modules/.vite/<app>

# 3. root에서 재설치
cd framework-ts
npm ci
```

---

## Root package.json

파일: `framework-ts/package.json`

```json
{
  "name": "devian-framework-ts",
  "version": "10.0.0",
  "type": "module",
  "private": true,
  "workspaces": [
    "module/*",
    "apps/*",
    "tools/*"
  ],
  "scripts": {
    "builder": "npm -w builder run build --",
    "archive": "npm -w archive run archive --",
    "dev:client": "npm -w game-client run dev",
    "start:server": "npm -w game-server run start"
  }
}
```

### Scripts 사용법

| 명령어 | 설명 |
|--------|------|
| `npm run builder -- ../{buildInputJson}` | 빌드 실행 (예: `npm run builder -- ../input/build_input.json`) |
| `npm run archive -- <args>` | 프로젝트 아카이브 |
| `npm run dev:client` | 클라이언트 개발 서버 |
| `npm run start:server` | 게임 서버 시작 |

---

## tsconfig.json

파일: `framework-ts/tsconfig.json`

| 키 | 값 |
|----|-----|
| `target` | `ES2020` |
| `module` | `ESNext` |
| `moduleResolution` | `node` |
| `baseUrl` | `.` |
| `paths.@devian/core` | `["./module/devian/src"]` |

---

## npm ci Contract

CI/CD 환경에서는 `npm ci`를 사용한다.

```bash
cd framework-ts
npm ci
```

### Recovery (lockfile 손상 시)

```bash
cd framework-ts
rm -rf node_modules package-lock.json
npm install
```

---

## See Also

- Archive: `skills/devian/80-tools/90-project-archive/SKILL.md`
- SSOT: `skills/devian/10-module/03-ssot/SKILL.md`
- Build: `skills/devian/80-tools/11-builder/20-build-pipeline/SKILL.md`
