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
- When analyzing or working with a skill group, **always read `00-overview/SKILL.md` first** to understand:
  - What the group covers
  - Key documents and their purposes
  - Links to related skills and SSOT
- Entry points:
  - `skills/SKILL.md` — Root index
  - `skills/devian/SKILL.md` — Devian main index (includes SSOT Hub)
- SSOT hierarchy:
  - `skills/devian-core/03-ssot/SKILL.md` — **Root SSOT** (공통 용어, 플레이스홀더, 입력 분리, 머지 규칙)
  - `skills/devian-tools/03-ssot/SKILL.md` — Tools SSOT (빌드 파이프라인, Phase, Validate, tempDir)
  - `skills/devian-builder/03-ssot/SKILL.md` — Builder SSOT (tableConfig, Tables, NDJSON, pb64, Protocol Spec, Opcode/Tag, Protocol UPM)
  - `skills/devian-unity/03-ssot/SKILL.md` — Unity SSOT (upmConfig, UPM Sync, Foundation)
  - `skills/devian-examples/03-ssot/SKILL.md` — Examples SSOT (config/input JSON, TS apps, Unity Example)
