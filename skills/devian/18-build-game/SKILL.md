# 18-build-game

Status: ACTIVE  
AppliesTo: v10  
Type: Policy / Build Configuration

## SSOT

이 문서는 **Game 도메인/프로토콜 빌드 설정 정책**을 정의한다.

---

## 개요

Game 빌드는 `input/input_common.json`으로 실행한다.

- `input_common.json`에 `domains.Game` + `protocols.group=Game` 포함
- 모든 생성물(C#/TS/UPM)은 표준 경로로 출력
- `input/temp/`는 staging 폴더로, **repo에 커밋 금지**

---

## 빌드 실행

```bash
# 빌드 실행
bash input/build.sh input/input_common.json

# 또는 직접 실행
node framework-ts/tools/builder/build.js input/input_common.json
```

---

## input_common.json 구조 (Game 관련)

```json
{
  "version": "10",
  "configPath": "./config.json",
  "tempDir": "temp",
  "domains": {
    "Common": { ... },
    "Game": {
      "contractDir": "Game/contracts",
      "contractFiles": ["*.json"],
      "tableDir": "Game/tables",
      "tableFiles": ["*.xlsx"]
    }
  },
  "protocols": [
    {
      "group": "Game",
      "protocolDir": "./Protocols/Game",
      "protocolFiles": ["C2Game.json", "Game2C.json"]
    }
  ]
}
```

---

## 산출물 경로

| 타입 | 경로 |
|------|------|
| C# Domain | `framework-cs/module/Devian.Domain.Game/` |
| C# Protocol | `framework-cs/module/Devian.Protocol.Game/` |
| TS Domain | `framework-ts/module/devian-module-game/` |
| TS Protocol | `framework-ts/module/devian-network-game/` |
| UPM Domain | `framework-cs/upm/com.devian.domain.game/` |
| UPM Protocol | `framework-cs/upm/com.devian.protocol.game/` |
| Table (ndjson/pb64) | `output/table/Game/` |

---

## Hard Rules

### 1. tempDir 정리

- `input/temp/`는 빌드 staging 폴더
- 빌더가 매 실행마다 clean 후 재생성
- **repo에 커밋 금지** (있으면 삭제)

### 2. 빌드 후 검증

빌드 완료 후 아래가 존재해야 함:

- `framework-cs/module/Devian.Domain.Game/Devian.Domain.Game.csproj`
- `framework-cs/module/Devian.Protocol.Game/Devian.Protocol.Game.csproj`
- `framework-ts/module/devian-module-game/package.json`
- `framework-ts/module/devian-network-game/package.json`
- `framework-cs/upm/com.devian.domain.game/package.json`
- `framework-cs/upm/com.devian.protocol.game/package.json`

---

## Reference

- 빌더: `framework-ts/tools/builder/build.js`
- 빌드 설정: `input/input_common.json`
- Related: `03-ssot/SKILL.md`
