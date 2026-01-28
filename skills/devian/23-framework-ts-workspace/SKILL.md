# 23-framework-ts-workspace

Status: ACTIVE
AppliesTo: v10
SSOT: skills/devian/03-ssot/SKILL.md

## Purpose

Devian v10의 TypeScript(workspaces) 구성에서 **node_modules 단일 공유**와
루트 실행 방식을 고정한다.

---

## Hard Rules

### 단일 Workspace Root

- Workspace root는 **`framework-ts/package.json` 단 하나**만 허용한다.
- `framework-ts/tools/package.json`, `framework-ts/module/package.json` 같은 **중첩 workspace 루트는 금지**한다.

### 단일 Lockfile

- Lockfile은 **`framework-ts/package-lock.json` 단 하나**만 허용한다.
- 하위 폴더(`framework-ts/tools/package-lock.json` 등)의 lockfile은 금지한다.

### 단일 node_modules

- 의존성 설치는 **`framework-ts/node_modules`만** 사용한다.
- 하위 폴더에서 `npm install`을 실행해 `tools/node_modules` 등을 생성하는 행위는 금지한다.

---

## Workspace Coverage

루트 workspaces는 아래를 포함한다:

- `module/*`
- `apps/*`
- `tools/*`

---

## Standard Commands (Root)

`cd framework-ts` 이후:

- Builder: `npm run builder -- ../input/input_common.json`
- Archive: `npm run archive -- <args>`
- GameClient: `npm run dev:client`
- GameServer: `npm run start:server`

---

## build.sh Bootstrap Policy

- `input/build.sh`는 `framework-ts/` 루트에서만 npm bootstrap을 수행해야 한다.
- `framework-ts/tools` 아래에 node_modules를 생성하는 방식은 금지한다.
