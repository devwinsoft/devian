# Devian v10 — Common Feature: Log

Status: ACTIVE  
AppliesTo: v10  
SSOT: skills/devian/10-module/03-ssot/SKILL.md

## Overview

Devian core의 표준 로깅 기능을 정의한다.

목표:
- 서버(C#) / 클라이언트(TS)에서 동일한 개념(레벨, 메시지)을 제공한다.
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

1. 로깅 호출부는 `Console.WriteLine` / `console.*`를 직접 사용하지 않는다.  
   - 직접 호출은 **Sink 내부에서만 허용**한다.
2. 기본 Sink는 존재해야 하며(기본 동작 보장), Sink 교체도 가능해야 한다.
3. 로그 레벨은 정확히 4단계만 제공한다: Debug / Info / Warn / Error
4. **모든 출력 API는 message 파라미터 1개만 받는다.** (tag, ex 파라미터 금지)

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
        void Write(LogLevel level, string message);
    }

    public static class Log
    {
        // 설정
        public static void SetLevel(LogLevel level);
        public static LogLevel GetLevel();

        public static void SetSink(ILogSink sink);
        public static ILogSink GetSink();

        // 출력 (message 1개만)
        public static void Debug(string message);
        public static void Info(string message);
        public static void Warn(string message);
        public static void Error(string message);
    }
}
```

필수 기본 구현:
- `ConsoleLogSink : ILogSink` (기본 Sink)
- 기본 포맷: `[{LEVEL}] {message}`

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
  write(level: LogLevel, message: string): void;
}

export function setLevel(level: LogLevel): void;
export function getLevel(): LogLevel;

export function setSink(sink: LogSink): void;
export function getSink(): LogSink;

// output (message 1개만)
export function debug(message: string): void;
export function info(message: string): void;
export function warn(message: string): void;
export function error(message: string): void;
```

필수 기본 구현:
- `ConsoleLogSink` (기본 Sink)
- Sink 내부에서만 `console.debug/info/warn/error` 사용

---

## Examples

### C#

```csharp
using Devian;

Log.SetLevel(LogLevel.Info);

Log.Debug("this will be filtered out");
Log.Info("connected");
Log.Warn("token expired soon");
Log.Error($"query failed: {ex}");
```

### TypeScript

```typescript
import { setLevel, LogLevel, info, warn, error } from "@devian/core";

setLevel(LogLevel.Info);

info("connected");
warn("token expired soon");
error(`query failed: ${err}`);
```

---

## DoD (Definition of Done)

- [x] C# Log 구현 + 기본 Sink 구현
- [x] TS logger.ts 구현 + 기본 Sink 구현
- [ ] 레벨 필터링 동작 확인
- [ ] Sink 교체 동작 확인
- [ ] (TS) features/index.ts export가 빌더에 의해 자동 반영됨을 확인
