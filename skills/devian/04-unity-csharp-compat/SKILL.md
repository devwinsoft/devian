# 04-unity-csharp-compat — Unity C# 문법/언어버전 제한

Status: ACTIVE  
AppliesTo: v10  
SSOT: this file

## Purpose

Unity(UPM/Packages 포함)에서 컴파일 깨짐을 원천 방지하기 위한 C# 문법 및 언어버전 제한 정책을 정의한다.

---

## Policy (정책)

### A) 적용 범위 (Scope)

이 정책은 **Unity에서 컴파일되는 모든 C# 코드**에 적용된다.

**적용 대상 경로:**

| 경로 | 설명 |
|------|------|
| `framework-cs/upm-src/**` | 수동 관리 UPM 패키지 |
| `framework-cs/apps/UnityExample/Packages/**` | Unity 최종 패키지 |
| `framework-cs/apps/**/Packages/**` | 추가 Unity 앱/샘플 패키지 |

> 적용 대상은 수동 코드 + 생성물 모두 포함한다.
> UPM 패키지 내부의 `Samples~/`, `Templates/` 및 패키지로 복사된 샘플 코드도 동일하게 적용 대상이다.

### B) 언어 버전 고정 (Language Level)

**Hard Rule:**
- Unity 대상 C# 코드는 **C# 8 호환 문법만 사용**한다.
- 최신 C# 기능을 "추측으로" 사용하지 않는다.
- Unity 버전에 따라 지원되지 않는 문법은 **컴파일 실패**를 유발한다.

### C) 금지 문법 목록 (Forbidden — 발견 시 FAIL)

아래 문법은 **Unity C# 적용 범위에서 하드 금지**이며, 발견 시 빌드/정책상 **FAIL**이다.

| 금지 문법 | 설명 | 예시 |
|-----------|------|------|
| Primary constructor | class/struct/record 헤더에 `(` 붙는 형태 | `class X(...)`, `struct X(...)` |
| `record`, `record struct` | 레코드 타입 | `record Person(...)` |
| `required` 멤버 | 필수 속성 한정자 | `required string Name` |
| File-scoped namespace | 세미콜론으로 끝나는 namespace | `namespace X;` |
| `global using` | 전역 using | `global using System;` |
| Target-typed `new()` | 타입 생략된 new | `List<int> x = new();` |
| `init` accessor | init-only setter | `{ get; init; }` |
| Collection expression | 대괄호 컬렉션 생성 | `int[] arr = [1, 2, 3];` |
| List pattern | 리스트 패턴 매칭 | `x is [1, 2, ..]` |
| Raw string literal | 삼중 따옴표 문자열 | `"""..."""` |
| **Delegate 식별자에 `?`** | delegate 선언의 이름에 `?` 붙이기 | `delegate void Handler?(int x)` |
| **타입 표기에 `??`** | 타입/멤버 선언부에서 `??` 사용 | `Handler??`, `Action??` |

**원칙:** Unity에서 불확실한 최신 문법은 금지한다.

**Nullable/Delegate 문법 하드 금지:**
- Delegate 선언에서 식별자(이름)에 `?`를 붙이는 것은 문법 오류다. nullable 표기는 참조 사용 위치에서만 의미가 있다.
- 타입 표기에서 `??`가 만들어지는 패턴은 문법 오류다. `??`는 표현식 연산자이며 타입/멤버 선언부에 등장하면 컴파일 실패한다.

### D) 표현식 vs 선언 규칙 (CS1519 방지)

**Hard Rule:**
- `??`, `??=` 등 null-coalescing 계열은 **메서드/표현식 내부에서만** 사용한다.
- 필드/프로퍼티/클래스 본문(멤버 선언 영역)에 "표현식 조합"을 넣는 형태는 금지한다.
- 애매한 경우 로컬 변수/메서드에서 처리한다.

**금지 예시 (delegate 선언에 `?` 붙이기):**
```csharp
// ❌ WRONG - delegate 선언에 ? 붙임
public delegate void MyHandler?(int x);

// ❌ WRONG - 프로퍼티 타입에 ?? 중복
public MyHandler?? OnEvent { get; set; }
```

**올바른 예시:**
```csharp
// ✅ CORRECT - delegate 선언은 ? 없이
public delegate void MyHandler(int x);

// ✅ CORRECT - nullable 프로퍼티는 타입에 ? 하나만
public MyHandler? OnEvent { get; set; }

// ✅ CORRECT - ?? 는 메서드 내부에서 사용
public void DoSomething()
{
    var handler = OnEvent ?? DefaultHandler;
}
```

---

## Verification (검증)

### 금지 패턴 검사 (Hard Gate)

아래 정규식 패턴이 적용 범위 경로에서 1개라도 발견되면 **FAIL**:

| 패턴 | 탐지 대상 |
|------|-----------|
| `\bclass\s+\w+\s*\(` | class primary constructor |
| `\bstruct\s+\w+\s*\(` | struct primary constructor |
| `\brecord\b` | record 타입 |
| `\brequired\b` | required 멤버 |
| `^\s*namespace\s+.*;\s*$` | file-scoped namespace |
| `\bglobal\s+using\b` | global using |
| `\bnew\s*\(\s*\)\s*;?` | target-typed new |
| `\binit\s*;` | init accessor |
| `\bdelegate\b[^{;]*\w+\s*\?\s*\(` | delegate 식별자에 `?` 붙은 경우 |
| `\b[A-Za-z_][A-Za-z0-9_]*\s*\?\?\b` | 타입/선언부에서 `??` 패턴 |

> 패턴 `\b[A-Za-z_][A-Za-z0-9_]*\s*\?\?\b`가 false positive를 내면, 해당 코드를 메서드 내부 표현식으로 옮기고 선언부에서 제거한다.

**검사 대상 경로:**
- `framework-cs/upm-src/`
- `framework-cs/apps/**/Packages/`
- UPM 패키지 내부의 `Samples~/` 및 템플릿/샘플 코드도 검사 대상에 포함한다.

### DoD (Definition of Done)

- [ ] 적용 범위 경로에서 금지 패턴 발견 시 빌드 **FAIL**
- [ ] 모든 Unity C# 코드가 C# 8 호환 문법만 사용

---

## Examples (예시)

### 좋은 예

```csharp
// ✅ Block-scoped namespace
namespace Devian
{
    // ✅ 일반 클래스 선언
    public sealed class NetClient
    {
        private readonly INetRuntime _runtime;
        
        // ✅ nullable 프로퍼티
        public Action<int>? OnError { get; set; }
        
        // ✅ 생성자
        public NetClient(INetRuntime runtime)
        {
            _runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
        }
    }
}
```

### 나쁜 예

```csharp
// ❌ File-scoped namespace
namespace Devian;

// ❌ Primary constructor
public class NetClient(INetRuntime runtime)
{
}

// ❌ Target-typed new
private readonly List<int> _list = new();

// ❌ Init accessor
public string Name { get; init; }

// ❌ Required member
public required string Id { get; set; }
```

---

## Reference

- SSOT: `skills/devian/03-ssot/SKILL.md`
- 적용 대상: `framework-cs/upm-src/`, `framework-cs/apps/**/Packages/`
