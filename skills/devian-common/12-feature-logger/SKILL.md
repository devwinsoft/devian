# Devian v10 — Common Feature: Logger

Status: DRAFT  
AppliesTo: v10  
SSOT: skills/devian/03-ssot/SKILL.md

## Overview

Common 모듈의 표준 로깅 기능을 정의한다.

목표:
- 서버(C#) / 클라이언트(TS)에서 동일한 개념(레벨, 태그, 메시지)을 제공한다.
- 기본 구현은 콘솔 출력이지만, Sink 교체로 출력 대상을 바꿀 수 있어야 한다.

---

## Responsibilities

- 4단계 로그 레벨 제공: Debug / Info / Warn / Error
- 전역(정적) 진입점 제공 (사용처에서 DI 강제하지 않음)
- 레벨 필터링 제공
- 출력 대상(Sink) 교체 가능

---

## Non-goals

- 구조화 로깅(JSON), 트레이싱, 분산 추적
- 외부 로깅 프레임워크(Serilog/Winston 등) 강제
- 파일 로깅/롤링/원격 전송 같은 고급 Sink 제공 (앱 책임)

---

## Hard Rules (MUST)

1. **Common은 다른 모듈을 참조하지 않는다.** (skills/devian-common/01-module-policy 준수)
2. 로깅 호출부는 `Console.WriteLine` / `console.*`를 직접 사용하지 않는다.  
   - 직접 호출은 **Sink 내부에서만 허용**한다.
3. 기본 Sink는 존재해야 하며(기본 동작 보장), Sink 교체도 가능해야 한다.
4. 로그 레벨은 정확히 4단계만 제공한다: Debug / Info / Warn / Error

---

## Public API

### C#

```csharp
namespace Devian
{
    public enum LogLevel
    {
        Debug = 10,
        Info = 20,
        Warn = 30,
        Error = 40,
    }

    public interface ILogSink
    {
        void Write(LogLevel level, string tag, string message, System.Exception? ex = null);
    }

    public static class Logger
    {
        // 설정
        public static void SetLevel(LogLevel level);
        public static LogLevel GetLevel();

        public static void SetSink(ILogSink sink);
        public static ILogSink GetSink();

        // 출력
        public static void Debug(string tag, string message);
        public static void Info(string tag, string message);
        public static void Warn(string tag, string message);
        public static void Error(string tag, string message, System.Exception? ex = null);
    }
}
```

필수 기본 구현:
- `ConsoleLogSink : ILogSink` (기본 Sink)
- 기본 포맷(권장): `[{LEVEL}] {tag} - {message}` (+ 예외는 별도 출력 가능)

### TypeScript (`devian-domain-common/features`)

```typescript
// features/logger.ts

export enum LogLevel {
  Debug = 10,
  Info = 20,
  Warn = 30,
  Error = 40,
}

export interface LogSink {
  write(level: LogLevel, tag: string, message: string, err?: unknown): void;
}

export function setLevel(level: LogLevel): void;
export function getLevel(): LogLevel;

export function setSink(sink: LogSink): void;
export function getSink(): LogSink;

// output
export function debug(tag: string, message: string): void;
export function info(tag: string, message: string): void;
export function warn(tag: string, message: string): void;
export function error(tag: string, message: string, err?: unknown): void;
```

필수 기본 구현:
- `ConsoleLogSink` (기본 Sink)
- Sink 내부에서만 `console.debug/info/warn/error` 사용

---

## Examples

### C#

```csharp
using Devian;

Logger.SetLevel(LogLevel.Info);

Logger.Debug("Net", "this will be filtered out");
Logger.Info("Net", "connected");
Logger.Warn("Auth", "token expired soon");
Logger.Error("DB", "query failed", ex);
```

### TypeScript

```typescript
import { setLevel, LogLevel, info, warn, error } from "@devian/module-common/features";

setLevel(LogLevel.Info);

info("Net", "connected");
warn("Auth", "token expired soon");
error("DB", "query failed", err);
```

---

## DoD (Definition of Done)

- [x] C# Logger 구현 + 기본 Sink 구현
- [x] TS logger.ts 구현 + 기본 Sink 구현
- [ ] 레벨 필터링 동작 확인
- [ ] Sink 교체 동작 확인
- [ ] (TS) features/index.ts export가 빌더에 의해 자동 반영됨을 확인
