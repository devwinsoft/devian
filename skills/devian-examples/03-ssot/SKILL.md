# devian-examples — SSOT

Status: ACTIVE
AppliesTo: v10
Type: SSOT

## Purpose

Devian Example 앱의 **설정 파일, 입력 파일, 런타임 앱 경로**를 정의하는 SSOT 문서.

---

## Config / Input JSON (SSOT)

| 파일 | 경로 | 역할 |
|------|------|------|
| **config.json** | `input/config.json` | 빌드 출력 경로 설정 (csConfig, tsConfig, upmConfig, tableConfig) |
| **input_common.json** | `input/input_common.json` | 빌드 입력 정의 (domains, protocols, tempDir) |

### config.json 구조

```json
{
  "configVersion": 1,
  "csConfig": { "moduleDir": "../framework-cs/module" },
  "tsConfig": { "moduleDir": "../framework-ts/module" },
  "upmConfig": {
    "sourceDir": "../framework-cs/upm",
    "packageDir": "../framework-cs/apps/UnityExample/Packages"
  },
  "tableConfig": {
    "soundDirs": ["../framework-cs/apps/UnityExample/Assets/Bundles/Sounds"],
    "stringDirs": ["../framework-cs/apps/UnityExample/Assets/Bundles/Strings"],
    "tableDirs": ["../framework-cs/apps/UnityExample/Assets/Bundles/Tables"]
  },
  "samplePackages": ["com.devian.samples"]
}
```

### input_common.json 구조

```json
{
  "version": "10",
  "configPath": "./config.json",
  "tempDir": "temp",
  "domains": {
    "Common": { "contractDir": "...", "tableDir": "...", "stringDir": "..." },
    "Game": { "contractDir": "...", "tableDir": "...", "stringDir": "..." },
    "Sound": { "contractDir": "...", "tableDir": "...", "stringDir": "..." }
  },
  "protocols": [
    { "group": "Game", "protocolDir": "./Protocols/Game", "protocolFiles": ["C2Game.json", "Game2C.json"] }
  ]
}
```

---

## Example Apps (SSOT)

### TypeScript Apps

| App | 경로 | 역할 |
|-----|------|------|
| **GameServer** | `framework-ts/apps/GameServer/` | WebSocket 서버 (Game protocol) |
| **GameClient** | `framework-ts/apps/GameClient/` | WebSocket 클라이언트 (Game protocol) |

#### GameServer 구조

```
framework-ts/apps/GameServer/
├── src/
│   └── index.ts          # 서버 엔트리 (WsTransport + NetworkServer)
├── package.json
└── tsconfig.json
```

**핵심 의존성:**
- `@devian/core` — NetworkServer, WsTransport
- `@devian/protocol-game/server-runtime` — Game protocol stub/proxy

#### GameClient 구조

```
framework-ts/apps/GameClient/
├── src/
│   └── index.ts          # 클라이언트 엔트리 (NetworkClient + WebSocket)
├── package.json
└── tsconfig.json
```

**핵심 의존성:**
- `@devian/core` — NetworkClient
- `@devian/protocol-game/client-runtime` — Game protocol stub/proxy

### Unity Example

| 항목 | 경로 |
|------|------|
| **프로젝트 루트** | `framework-cs/apps/UnityExample/` |
| **메인 씬** | `framework-cs/apps/UnityExample/Assets/Scenes/SampleScene.unity` |
| **UPM 패키지** | `framework-cs/apps/UnityExample/Packages/com.devian.*` |
| **번들 에셋** | `framework-cs/apps/UnityExample/Assets/Bundles/` |

---

## 실행 방법

### TypeScript Apps

```bash
# GameServer 실행
npm -w GameServer run start

# GameClient 실행 (별도 터미널)
npm -w GameClient run start
```

### Unity Example

1. Unity Hub에서 `framework-cs/apps/UnityExample` 프로젝트 열기
2. `Assets/Scenes/SampleScene.unity` 씬 로드
3. Play 버튼으로 실행

---

## Related

- Config/Input 상세: `skills/devian-core/03-ssot/SKILL.md`
- Protocol 빌드: `skills/devian-builder/03-ssot/SKILL.md`
- UPM 패키지: `skills/devian-unity/01-policy/SKILL.md`
