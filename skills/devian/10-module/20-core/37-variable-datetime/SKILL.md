# Devian v11 — Variable: CDateTime

Status: ACTIVE
AppliesTo: v11
SSOT: skills/devian/10-module/03-ssot/SKILL.md

## Purpose

`CDateTime`은 UTC epoch millisecond와 `DateTime`을 함께 유지하는 단순 공용 변수 타입이다.

- 입력 기준: `utcTimeMs` (long, Unix epoch ms)
- 결과: `dateTime` (UTC)

---

## Hard Rules

1. client/server 구분 필드(`server*`, `client*`)와 diff/helper API를 두지 않는다.
2. `utcTimeMs`는 `DateTime` 표현 가능 범위로 clamp한다.
3. `dateTime`은 항상 UTC (`DateTimeOffset.FromUnixTimeMilliseconds(...).UtcDateTime`)로 생성한다.
4. `SetDateTime(DateTime)` 입력이 UTC가 아니면 UTC로 변환 후 저장한다.
5. `CDateTime(string)`은 문자열에서 숫자만 추출해 순서대로 `year(4) -> month(2) -> day(2) -> hour(2) -> minute(2) -> second(2) -> millisecond(3)`를 채운다.
6. 문자열 파싱에서 값이 없으면 `0`으로 처리하고, 최종 `DateTime` 생성 시 유효 범위로 clamp한다.

---

## Public API

```csharp
[Serializable]
public struct CDateTime : IEquatable<CDateTime>
{
    public long utcTimeMs;
    public DateTime dateTime;

    public CDateTime(long utcTimeMs);
    public CDateTime(DateTime dt);
    public CDateTime(string raw);
    public void Initialize(long utcTimeMs);
    public void SetUtcTimeMs(long utcTimeMs);
    public void SetDateTime(DateTime dateTime);
    public static DateTime ToUtcDateTime(long utcTimeMs);
}
```

---

## Files (3-path)

- `framework-cs/module/Devian/src/Variable/CDateTime.cs`
- `framework-cs/upm/com.devian.foundation/Runtime/Module/Variable/CDateTime.cs`
- `framework-cs/apps/UnityExample/Packages/com.devian.foundation/Runtime/Module/Variable/CDateTime.cs`

---

## Table Authoring

XLSX 테이블에서 `class:CDateTime` 타입 컬럼으로 사용한다.

- 셀에 datetime 문자열 작성 (예: `2024-03-08 15:30:45.123`)
- 빌드 시 `CDateTime(string raw)` 파싱 로직을 재현하여 `utcTimeMs` long 값으로 변환
- NDJSON/pb64에는 `utcTimeMs` long 값만 저장

DFF 규약: `skills/devian/80-tools/11-builder/30-table-cell-format/SKILL.md`

---

## DoD

- [ ] `CDateTime`가 3-path 모두 동일 구현으로 존재
- [ ] client/server 구분 및 diff/helper API 제거
- [ ] `utcTimeMs`, `dateTime` 단일 모델 유지
- [ ] `DateTime`, `string` 생성자 2개 제공
- [ ] 테스트 코드는 추가하지 않음 (요청 사항)

---

## Reference

- Parent: `skills/devian/10-module/20-core/00-overview/SKILL.md`
