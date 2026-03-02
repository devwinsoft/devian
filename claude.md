# Claude Work Policy (Prevent Repeat Mistakes)

## 1) No guessing
- Do not assume types/APIs. Always locate the actual definition in the repo and use only confirmed members.
- If unsure, use `grep` or `view` to find the real implementation first.

## 2) C# edit basics
- If you use any LINQ method (`ToList`, `First`, `Any`, `Select`, `Where`, etc.), ensure:
  - `using System.Linq;`
- If you touch Newtonsoft JSON types (`JObject`/`JToken`/`JProperty`), ensure:
  - `using Newtonsoft.Json.Linq;`

## 3) Ban ambiguous symbols (especially Value)
- Only these `Value` patterns are allowed:
  - `JObject.Value<T>("key")`
  - `JToken.Value<T>()`
- Never call `Value<T>()` on collections like `IEnumerable<JToken>` or `IEnumerable<JProperty>`.
- If there is any ambiguity, use fully-qualified calls:
  - `System.Linq.Enumerable.ToList(...)`

## 4) Compile check after changes
- If you edited any C# file, run one compile/build step and confirm 0 errors.
- If you cannot build, at minimum re-check:
  - required `using` directives
  - name conflicts (`Extensions`, `Value`, `ToList`)

## 5) Follow SSOT (skills) strictly
- Do not change file names/paths/extensions or formats unless the Skill (SSOT) is updated first.
- If a rule must change, update the Skill first, then update implementation to match.

## 6) Skills folder structure
- All `skills/devian-*` folders follow this structure:
  - `00-overview/SKILL.md` — Group overview (what this group contains)
  - `01-policy/SKILL.md` — Group policy/rules
  - `03-ssot/SKILL.md` — Category SSOT (if applicable: core, tools, data, protocol, unity)

### 6-1) 스킬 읽기 순서 (필수)
- 작업 대상 스킬 그룹에 진입할 때, **반드시 아래 순서로 먼저 읽는다:**
  1. **상위 그룹의 `01-policy/SKILL.md`** — 해당 카테고리의 하드룰/정책 확인
  2. **상위 그룹의 `03-ssot/SKILL.md`** — 공통 SSOT/동기화 규칙 확인
  3. **작업 대상 그룹의 `00-overview/SKILL.md`** — 그룹 개요, 문서 라우팅
  4. **작업 대상 그룹의 `01-policy/SKILL.md`** (있으면) — 그룹별 정책
  5. **작업 대상 그룹의 `03-ssot/SKILL.md`** (있으면) — 그룹별 SSOT
- 예시: `50-mobile-system/30-purchase-system/` 작업 시
  - `devian-unity/01-policy` → `devian-unity/03-ssot` → `30-purchase-system/00-overview` → `30-purchase-system/01-policy` → `30-purchase-system/03-ssot`
- **이 순서를 지키지 않으면 상위 정책(3-path mirror, UPM sync 등)을 모르고 하위 스킬을 수정하는 실수가 발생한다.**

### 6-2) 교차 관심사 (Cross-cutting concerns)
- **3-path mirror**: `com.devian.samples` Samples~ 파일은 3곳에 미러링된다.
  - 정본: `devian-unity/07-samples-creation-guide/SKILL.md`
  - 동기화 규칙: `devian-unity/03-ssot/SKILL.md` §UPM Packages Sync
  - 하위 스킬에서는 상위 정책을 **참조만** 하고, 규칙을 중복 정의하지 않는다.
- **Implementation Location 표기 규칙**:
  - 섹션 제목: `Implementation Location (3-path mirror)`
  - 경로별 역할: `UPM (정본)` / `Packages (sync)` / `Assets/Samples (import)`
  - Assets/Samples 경로의 버전은 `{version}` 플레이스홀더 사용 (하드코딩 금지)

- Entry points:
  - `skills/SKILL.md` — Root index
  - `skills/devian/SKILL.md` — Devian main index (includes SSOT Hub)
- SSOT hierarchy:
  - `skills/devian/10-module/03-ssot/SKILL.md` — **Root SSOT** (공통 용어, 플레이스홀더, 입력 분리, 머지 규칙)
  - `skills/devian/80-tools/03-ssot/SKILL.md` — Tools SSOT (빌드 파이프라인, Phase, Validate, tempDir)
  - `skills/devian/80-tools/11-builder/03-ssot/SKILL.md` — Builder SSOT (tableConfig, Tables, NDJSON, pb64, Protocol Spec, Opcode/Tag, Protocol UPM)
  - `skills/devian-unity/03-ssot/SKILL.md` — Unity SSOT (upmConfig, UPM Sync, Foundation)
  - `skills/devian-examples/03-ssot/SKILL.md` — Examples SSOT (config/input JSON, TS apps, Unity Example)

## 7) 역할 경계 — Claude는 도구이다

- Claude는 사용자의 요청을 실행하는 **도구**이다. 아키텍트가 아니다.
- SSOT/스킬 문서에 **새로운 개념·섹션·정책을 생성**하는 것은 사용자(아키텍트)의 권한이다.
- 요청 범위를 넘는 작업이 필요하다고 판단되면, **실행하지 않고 먼저 사용자에게 물어본다.**
- 구현을 먼저 하고 SSOT를 맞추는 것은 금지. (§5 위반)
