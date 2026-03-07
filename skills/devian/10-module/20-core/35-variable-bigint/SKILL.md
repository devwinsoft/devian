# Devian v11 — Feature: Complex BigInt


정규화 규칙:
- `mBase == 0` → `(mBase=0, mPow=0)`
- `abs(mBase) >= 10` 이면 `mBase /= 10`, `mPow++` 반복
- `abs(mBase) < 1` 이면 `mBase *= 10`, `mPow--` 반복


---


## Compare


- 부호가 다르면 양수 > 0 > 음수
- 부호가 같으면 `mPow` 우선 비교
- `mPow`가 같으면 `mBase` 비교


---


## Operators (source-compatible)


- `+` : `CBigInt + CBigInt`, `CBigInt + float`
- `-` : `CBigInt - CBigInt`, `float - CBigInt`, `CBigInt - float`
- `*` : `CBigInt * CBigInt`, `float * CBigInt`, `CBigInt * float`
- `/` : `CBigInt / CBigInt`, `float / CBigInt`, `CBigInt / float`
- 비교: `<, >, <=, >=` (CompareTo 기반)
- helper: `Zero`, `FromInt`, `FromLong`, `Max`, `Min`


---


## ToString (suffix)


- `mPow < 3`이면 정수 반올림 문자열
- `mPow >= 3`이면 3자리 단위로 suffix(symbol) 생성하여 표시
- suffix는 알파벳 기반 무한 확장(a..z, aa..)


---


## Example Code


```csharp
using System;


namespace Devian.Examples
{
    public static class ComplexBigIntExample
    {
        public static void Run()
        {
            var gold = new Devian.CBigInt(5.5f, 6);      // 5.5 * 10^6
            var reward = gold * 5f;
            var tax = reward / 10f;


            var seasonBonus = new Devian.CBigInt(2f, 3); // 2000
            var boosted = gold * seasonBonus;
            var total = gold + seasonBonus;


            if (boosted > gold)
            {
                Console.WriteLine("boosted is larger");
            }


            Console.WriteLine($"gold: {gold}");
            Console.WriteLine($"reward: {reward}");
            Console.WriteLine($"tax: {tax}");
            Console.WriteLine($"boosted: {boosted}");


            try
            {
                float f = gold;
                double d = boosted;
                Console.WriteLine($"float={f}, double={d}");
            }
            catch (OverflowException ex)
            {
                Console.WriteLine(ex.Message);
            }


            var spend = new Devian.CBigInt(7f, 5); // 700,000
            var remain = gold - spend;


            var plusSmall = gold + 123f;


            Console.WriteLine($"remain: {remain}");
            Console.WriteLine($"total: {total}");
            Console.WriteLine($"plusSmall: {plusSmall}");
        }
    }
}
```

---


## Table Authoring

`class:CBigInt`를 XLSX 테이블에 기입할 때 다음 형식을 지원한다.

- `{base, pow}` shorthand: `{5.5, 6}` → `5.5 * 10^6`, `{2, 3}` → `2000`
- plain long: `1000` → `1 * 10^3` (빌드 시 정규화)
- raw JSON fallback: `{"base":5.5,"pow":6}` 또는 최종 shape `{"mBase":{...},"mPow":{...}}`
