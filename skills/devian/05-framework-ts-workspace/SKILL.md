# Devian v10 — Framework-TS Workspace Policy

Status: ACTIVE
AppliesTo: v10
SSOT: this file

## Purpose

**`framework-ts`의 npm workspace 구성 정책**을 정의한다.

모든 app, module, tool이 **단일 루트 node_modules를 공유**하도록 workspace를 구성한다.

---

## Workspace 구조 (Hard Rule)

### 루트 Package.json

`framework-ts/package.json`은 **유일한 workspace 정의 파일**이다.

```json
{
  "name": "devian-framework-ts",
  "version": "10.0.0",
  "private": true,
  "workspaces": [
    "module/devian",
    "module/devian-domain-*",
    "module/devian-protocol-*",
    "apps/*",
    "tools/*"
  ],
  "devDependencies": {
    "typescript": "^5.3.0",
    "xlsx": "^0.18.5"
  }
}
```

### 하위 Package.json 규칙 (Hard Rule)

**하위 package.json에서 `workspaces` 필드 사용 금지.**

| 경로 | workspaces 필드 |
|------|----------------|
| `framework-ts/package.json` | ✅ 허용 (유일) |
| `framework-ts/module/package.json` | ❌ 금지 (삭제) |
| `framework-ts/tools/package.json` | ❌ 금지 (삭제) |
| `framework-ts/apps/*/package.json` | ❌ 금지 |
| `framework-ts/module/*/package.json` | ❌ 금지 |
| `framework-ts/tools/*/package.json` | ❌ 금지 |

### 중간 Wrapper Package.json (Hard Rule)

`module/package.json`과 `tools/package.json`은 **삭제하거나 빈 상태로 유지**한다.

**삭제 권장** - wrapper package.json은 불필요하며 혼란을 야기한다.

---

## Workspace 패키지 참조 규칙 (Hard Rule)

### 내부 패키지 참조

workspace 내 다른 패키지를 참조할 때는 **workspace 프로토콜**을 사용한다.

```json
{
  "dependencies": {
    "@devian/core": "workspace:*",
    "@devian/network-game": "workspace:*"
  }
}
```

**직접 버전 참조 금지:**

```json
{
  "dependencies": {
    "@devian/core": "10.0.0"  // ❌ 금지
  }
}
```

### 외부 패키지 참조

외부 패키지는 일반적인 버전 명시를 사용한다.

```json
{
  "dependencies": {
    "ws": "^8.14.0"
  }
}
```

---

## node_modules 위치 (Hard Rule)

**모든 dependencies는 `framework-ts/node_modules`에 설치**된다.

| 경로 | 허용 여부 |
|------|----------|
| `framework-ts/node_modules` | ✅ 유일한 node_modules |
| `framework-ts/apps/*/node_modules` | ❌ 금지 |
| `framework-ts/module/*/node_modules` | ❌ 금지 |
| `framework-ts/tools/*/node_modules` | ❌ 금지 |

하위 폴더에 node_modules가 존재하면 **삭제 대상**이다.

---

## 패키지 이름 규칙 (Hard Rule)

### Module 패키지

| 유형 | 이름 패턴 | 예시 |
|------|----------|------|
| Core Runtime | `@devian/core` | `@devian/core` |
| Domain Module | `@devian/module-{domainkey}` | `@devian/module-common`, `@devian/module-game` |
| Protocol Module | `@devian/network-{protocolgroup}` | `@devian/network-game` |

### App 패키지

| 유형 | 이름 패턴 | 예시 |
|------|----------|------|
| App | lowercase, no scope | `game-client`, `game-server` |

### Tool 패키지

| 유형 | 이름 패턴 | 예시 |
|------|----------|------|
| Tool | lowercase, no scope | `builder`, `archive` |

---

## npm install 규칙 (Hard Rule)

**항상 `framework-ts/` 루트에서 npm install 실행.**

```bash
cd framework-ts
npm install
```

하위 폴더에서 npm install 실행 금지:

```bash
cd framework-ts/apps/GameClient
npm install  # ❌ 금지
```

---

## Hard Conflicts (DoD)

아래 상태가 발견되면 **FAIL**:

1. 루트가 아닌 곳에 `workspaces` 필드 존재
2. 내부 패키지 참조에 직접 버전 사용 (`"@devian/core": "10.0.0"`)
3. 하위 폴더에 `node_modules` 디렉토리 존재
4. `module/package.json` 또는 `tools/package.json`에 `workspaces` 필드 존재

---

## Reference

- Policy SSOT: `skills/devian/03-ssot/SKILL.md`
- 동작 정본: 런타임/빌더 코드
