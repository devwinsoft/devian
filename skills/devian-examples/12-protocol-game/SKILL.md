# 12-protocol-game

Status: ACTIVE  
AppliesTo: v10

## SSOT

이 문서는 **Game 프로토콜 예제** (C2Game / Game2C)를 설명한다.

---

## 목표

- `ProtocolGroup = Game`의 프로토콜이 **최소 예제**임을 명시한다.
- C2Game / Game2C의 방향(Direction) 정의를 링크로 안내한다.
- 샘플 네트워크 템플릿에서 `Devian.Protocol.Game`을 연동하는 지점을 안내한다.

---

## Game 프로토콜 예제

**C2Game / Game2C는 Devian 프로토콜 시스템의 최소 작동 예제다.**

### 입력 파일

| 파일 | 경로 | 방향 |
|------|------|------|
| C2Game.json | `devian/input/Protocols/Game/C2Game.json` | Client → Server |
| Game2C.json | `devian/input/Protocols/Game/Game2C.json` | Server → Client |

### 방향(Direction) 정의

| 프로토콜 | 방향 | 설명 |
|----------|------|------|
| `C2Game` | Client → Server | 클라이언트가 서버로 보내는 메시지 |
| `Game2C` | Server → Client | 서버가 클라이언트로 보내는 메시지 |

> **상세 규칙:** `skills/devian-tools/11-builder/40-codegen-protocol/SKILL.md` 참조

### 빌드 생성물

예제를 빌드하면 아래 생성물이 만들어진다:

| 플랫폼 | 생성물 | 경로 |
|--------|--------|------|
| C# Module | `Devian.Protocol.Game` | `framework-cs/module/Devian.Protocol.Game/` |
| UPM Package | `com.devian.protocol.game` | `framework-cs/upm/com.devian.protocol.game/` |
| TS Module | `devian-protocol-game` | `framework-ts/module/devian-protocol-game/` |

---

## 샘플 네트워크 템플릿 연동

**Network 샘플 템플릿에서 `Devian.Protocol.Game`을 대체/연동하는 방법:**

### 연동 지점

1. **샘플 템플릿 위치**
   - `framework-cs/upm/com.devian.samples/Samples~/Network/`

2. **프로토콜 참조**
   - 샘플의 asmdef가 `Devian.Protocol.Game` 어셈블리를 참조
   - C2Game/Game2C 메시지 타입을 사용해 송수신

3. **사용자 커스텀**
   - 샘플을 Import 후 자신의 프로토콜로 대체 가능
   - `Devian.Protocol.Game` → 사용자 프로토콜로 교체

> **샘플 템플릿 상세:** `skills/devian-examples/14-unity-game-net-manager/SKILL.md` 참조

---

## 관련 스킬

| 주제 | 스킬 경로 |
|------|-----------|
| 프로토콜 코드젠 규칙 | `skills/devian-tools/11-builder/40-codegen-protocol/SKILL.md` |
| 프로토콜 생성 구현 | `skills/devian-tools/11-builder/44-protocolgen-implementation/SKILL.md` |
| Network 샘플 템플릿 | `skills/devian-examples/14-unity-game-net-manager/SKILL.md` |
| WebSocket 클라이언트 | `skills/devian/10-module/72-network-ws-client/SKILL.md` |
| 네트워크 서버 | `skills/devian/10-module/74-network-server/SKILL.md` |

---

## 금지

- 예제 프로토콜 스키마/내용 변경 금지
- 샘플 템플릿 구현 변경 금지 (문서 안내만)
- 새로운 빌드 규칙 추가 금지

---

## Reference

- Related: `skills/devian-examples/01-policy/SKILL.md`
- Related: `skills/devian-tools/11-builder/40-codegen-protocol/SKILL.md`
- Related: `skills/devian-examples/14-unity-game-net-manager/SKILL.md`
