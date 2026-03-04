# 14-version-number

Status: ACTIVE
AppliesTo: v10
SSOT: skills/devian/10-module/03-ssot/SKILL.md

## Purpose

`#.#.#` (Major.Minor.Patch) 형식의 버전 번호를 표현하는 불변 구조체.
비교 연산자와 문자열 파싱을 제공한다.

---

## Non-goals

- Pre-release 라벨 (e.g. `-alpha.1`)
- Build metadata (e.g. `+build123`)
- 버전 범위/제약 표현식 (e.g. `>=1.2.0 <2.0.0`)

---

## Data Structure

```csharp
[Serializable]
public struct VersionNumber : IComparable<VersionNumber>, IEquatable<VersionNumber>
{
    public int Major;
    public int Minor;
    public int Patch;
}
```

- Unity 직렬화 + PropertyDrawer 호환을 위해 mutable struct.
- 마스킹 불필요.
- 모든 컴포넌트는 >= 0. 음수 시 `ArgumentOutOfRangeException`.

---

## Construction

```csharp
public VersionNumber(int major, int minor, int patch)
```

---

## Parse / TryParse

```csharp
public static VersionNumber Parse(string value)
public static bool TryParse(string? value, out VersionNumber result)
```

규칙:
- 정확히 3개의 `.` 구분 세그먼트 필요 (`"1.2.3"`)
- 각 세그먼트는 non-negative 정수
- 선행 0 허용 (`"1.02.003"` → `1.2.3`)
- 공백 불허 (strict format)
- 실패 시 `Parse`는 `FormatException`, `TryParse`는 `false` 반환

---

## Comparison (IComparable)

비교 순서:
1. Major
2. Minor
3. Patch

## Equality (IEquatable)

세 컴포넌트 모두 일치해야 동일.

## Operators

| 연산자 | 위임 |
|--------|------|
| `<`, `>`, `<=`, `>=` | `CompareTo` |
| `==`, `!=` | `Equals` |

산술 연산자 없음.

---

## ToString

```csharp
public override string ToString() => $"{Major}.{Minor}.{Patch}";
```

---

## JSON Format

문자열: `"1.2.3"` (오브젝트 아님).
- Serialize: `ToString()`
- Deserialize: `Parse()`

---

## Files (SSOT)

```
framework-cs/module/Devian/src/Core/VersionNumber.cs          # C# module 정본
framework-cs/upm/com.devian.foundation/Runtime/Module/Core/   # C# UPM 미러
framework-ts/module/devian/src/versionNumber.ts               # TS 정본
```

---

## Reference

- Parent: `skills/devian/10-module/20-core/00-overview/SKILL.md`
- Policy: `skills/devian/10-module/01-policy/SKILL.md`
