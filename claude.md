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
