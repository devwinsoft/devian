# devian-unity/40-game-system — Policy

Status: ACTIVE
AppliesTo: v10
SSOT: skills/devian/10-module/03-ssot/SKILL.md

---

## 1. Templates 정의

**Templates**는 `com.devian.samples` UPM 패키지의 **Samples~** 폴더에 포함된 샘플 코드다.

- Unity Package Manager에서 "Import" 버튼으로 설치
- 설치 후 `Assets/Samples/`로 복사되어 프로젝트 소유가 됨
- 자유롭게 수정/삭제/확장 가능

---

## 2. 핵심 원칙

| 원칙 | 설명 |
|------|------|
| **단일 패키지** | 모든 템플릿은 `com.devian.samples` 패키지에 포함 |
| **Samples~ 방식** | Unity의 표준 샘플 제공 방식 사용 |
| **프로젝트 소유** | 설치 후 Assets/Samples/에 복사되어 프로젝트가 소유 |
| **충돌 방지** | `Devian` + `.Network.*`, `Devian` + `.Protocol.*`, `Devian` + `.Domain.*` 네임스페이스 사용 금지 |

정본/미러 동기화 하드룰은 상위 정책 `skills/devian-unity/01-policy/SKILL.md`를 따른다.

---

## 3. 네이밍 규칙

### 3.1 UPM 패키지명

```
com.devian.samples
```

단일 패키지에 모든 템플릿이 Samples~로 포함됨.

### 3.2 샘플 폴더명

```
Samples~/<TemplateName>/
```

예시:
- `Samples~/GameContents/`

### 3.3 asmdef 이름 (어셈블리명)

```
Devian.Samples.<TemplateName>
Devian.Samples.<TemplateName>.Editor
```

예시:
- `Devian.Samples.GameContents`
- `Devian.Samples.GameContents.Editor`

> **주의**: 위는 asmdef의 `name`(어셈블리명)이다. 코드의 namespace와 혼동하지 않는다.

### 3.4 namespace (Hard Rule)

**모든 샘플 코드는 단일 네임스페이스 `Devian`을 사용한다.**

```csharp
namespace Devian
```

> asmdef의 `rootNamespace`도 `"Devian"`으로 설정한다.

### 3.4.1 Protocol handlers 구현 정책 (Hard Rule)

**어셈블리 제약:**
`Devian.Protocol.*` 어셈블리에서 생성된 `*_Handlers.g.cs`는 **다른 asmdef(샘플 asmdef 포함)에서 partial로 확장할 수 없다.**
(C#의 partial class는 동일 어셈블리 내에서만 동작)

**샘플에서 수신 처리 구현 방식 (내부 생성 + partial 확장):**

1. **GameNetManager:** Stub/Proxy를 내부에서 생성/보관
   - `_stub = new Game2CStub()` — 생성자에서 내부 생성
   - `_proxy = new C2Game.Proxy(...)` — OnTransportCreated()에서 내부 생성
   - 외부 주입/등록 없음

2. **Game2CStub:** partial 클래스로 확장 가능
   - `OnPong()`, `OnEchoReply()` — 기본 로그 후 partial 훅 호출
   - `partial void OnPongImpl(...)` — 사용자가 별도 파일에서 구현
   - **"Sample" 접두어 금지** — `SampleGame2CStub` 같은 이름 사용 금지

3. **partial 확장 패턴:** 별도 파일에서 partial 메서드 구현
   ```csharp
   // Game2CStub.Partial.cs
   public partial class Game2CStub
   {
       partial void OnPongImpl(Game2C.EnvelopeMeta meta, Game2C.Pong message)
       {
           // Custom handling
       }
   }
   ```

**금지 (Hard):**
- 샘플에서 `partial class *_Handlers` 형태로 확장 시도 금지 (컴파일 불가, 오해 유발)
- 샘플에서 `namespace Devian.Protocol.*` 사용 금지 (Stub 상속 시에도 `namespace Devian` 사용)
- **"Sample" 접두어 타입명 금지** — `SampleGame2CStub`, `SampleProtocolHelper` 등 사용 금지
- **외부 Stub 주입/등록 금지** — RegisterStub(), inboundStub 사용 금지

### 3.5 금지 프리픽스 (네임스페이스)

샘플 코드에서 다음 네임스페이스 사용 금지:

- `Devian.Network.*`
- `Devian.Protocol.*` (샘플 어셈블리에서 사용 불가 — 3.4.1 참조)
- `Devian.Domain.*`
- `Devian.Core.*`
- `Devian.Templates.*` (샘플 코드 namespace로 사용 금지)
- `Devian.Samples.*` (샘플 코드 namespace로 사용 금지 — asmdef name으로만 사용)

> 어셈블리명(asmdef name)은 `Devian.Samples.*`를 사용하지만, namespace는 `Devian` 단일만 사용한다.

---

## 4. 경로 정책

### 4.1 Template 원본 (upm)

```
framework-cs/upm/com.devian.samples/
├── package.json
├── Samples~/
│   ├── GameContents/
│   │   ├── Runtime/
│   │   │   ├── [asmdef: Devian.Samples.GameContents]
│   │   │   └── Network/*.cs
│   │   ├── Editor/
│   │   │   ├── [asmdef: Devian.Samples.GameContents.Editor]
│   │   │   └── *.cs
│   │   └── README.md
│   └── (다른 템플릿들...)
└── README.md
```

### 4.2 빌드 출력 (packageDir)

```
{upmConfig.packageDir}/com.devian.samples/
```

### 4.3 설치 후 위치 (Unity 프로젝트)

```
Assets/Samples/Devian Templates/{version}/{TemplateName}/
```

---

## 5. FAIL 조건

- [ ] 샘플 패키지에 `Devian.Network.*`/`Devian.Protocol.*` namespace 코드가 존재
- [ ] 샘플에서 `partial class *_Handlers` 확장 시도 (컴파일 불가)
- [ ] package.json name이 `com.devian.samples`가 아님
- [ ] Samples~ 폴더가 없거나 비어있음
- [ ] samples 메타데이터가 package.json에 없음

---

## Related

- [07-samples-creation-guide](../../07-samples-creation-guide/SKILL.md)
- [Root SSOT](../../../devian/10-module/03-ssot/SKILL.md)
- [devian-unity/01-policy](../../01-policy/SKILL.md)
