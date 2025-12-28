# Devian – 40 WS Transport Adapter

## Purpose

**WebSocket transport의 한 가지 소비 패턴 예시를 설명한다.**

> Transport는 Consumer 책임이다.  
> Devian은 contracts 모듈만 제공하고, transport 구현을 제공하지 않는다.

---

## Scope

### In Scope

| 항목 | 설명 |
|------|------|
| contracts 사용 패턴 | DTO/opcode import 예시 |
| 언어별 예시 | C#, TypeScript (optional) |

### Out of Scope

| 항목 | 설명 |
|------|------|
| NestJS 템플릿 | Gateway/Module 템플릿 없음 |
| 서버 구조 규칙 | 강제하지 않음 |
| transport 라이브러리 | 제공하지 않음 |
| 언어 간 런타임 공유 | 존재하지 않음 |

---

## Hard Rules (MUST)

| # | Rule |
|---|------|
| 1 | 언어 간 직접 의존 ❌ |
| 2 | 공통 계약은 contracts에만 존재 |
| 3 | 각 언어 Consumer는 해당 언어 contracts만 import |

### Cross-Language = 병렬 소비 (Not 동기화)

```
C# Consumer ──────▶ contracts/csharp/{domain}
                         │
                    (동일한 계약)
                         │
TS Consumer ──────▶ contracts/ts/{domain}
```

- 언어 간 **직접 의존 없음**
- 각 Consumer는 **자기 언어 contracts만** 사용
- 공통 계약은 **contracts 정의에서만** 공유
- **"언어 간 공유 런타임"은 존재하지 않는다**

---

## Contracts Usage

### TypeScript (If Enabled)

> ⚠️ **If TypeScript codegen is enabled**:

```typescript
// NestJS 서버에서 (예시)
import { LoginRequestDto, Opcodes } from '@devian/auth';

// Gateway 구현은 서버 개발자 책임
@WebSocketGateway()
export class AuthGateway {
  @SubscribeMessage(Opcodes.LoginRequest)
  handleLogin(client: Socket, data: LoginRequestDto) {
    // 서버 로직
  }
}
```

**TS codegen이 비활성화된 경우, `contracts/ts/{domain}`은 존재하지 않을 수 있다.**

### C# (Unity/Server)

```csharp
// contracts/csharp/auth 사용
using Auth.Protocol;

public class AuthTransport
{
    public void Send(LoginRequestDto dto)
    {
        var bytes = Serialize(dto);
        webSocket.Send(bytes);
    }
}
```

---

## Transport Is Consumer Responsibility

| Consumer | 책임 |
|----------|------|
| Unity | WebSocket 클라이언트 구현 |
| NestJS | Gateway/Module 구성 |
| Browser | WebSocket API 사용 |

**Devian은 transport 구현을 제공하지 않는다.**

---

## Module Import Options (TS)

서버가 contracts/ts 모듈을 가져오는 방식은 자유다:

| 방식 | 설명 |
|------|------|
| workspace | pnpm/yarn workspace |
| git | git dependency |
| copy | 빌드 산출물 복사 |
| publish | npm registry |

---

## Soft Rules (SHOULD)

| # | Rule |
|---|------|
| 1 | 서버는 contracts를 복사하거나 vendoring할 수 있다 |
| 2 | 서버마다 contracts 사용 방식이 달라도 된다 |
| 3 | Multi-runtime을 자연스럽게 허용 |

---

## Acceptance Criteria

| # | 조건 |
|---|------|
| 1 | 언어 간 coupling을 권장하는 문장이 남아 있지 않다 |
| 2 | Devian이 multi-runtime을 자연스럽게 허용하는 구조로 보인다 |
| 3 | TS가 없는 프로젝트에서도 문서가 논리적으로 성립한다 |
| 4 | Transport가 Consumer 책임임이 명확하다 |

---

## Related Skills

| Skill | 관계 |
|-------|------|
| `22-codegen-protocol-csharp-ts` | Protocol codegen |
| `41-ws-dispatcher-skeleton` | Dispatcher 예시 |
| `51-generated-integration` | Generated 통합 |
| `90-language-first-contracts` | 경로 기준 |

---

## Version History

| Version | Date | Changes |
|---------|------|---------|
| 1.0.0 | 2025-12-28 | Initial |
