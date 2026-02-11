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


- `+` : `CBigInt + float`
- `-` : `CBigInt - CBigInt`, `float - CBigInt`, `CBigInt - float`
- `*` : `CBigInt * CBigInt`, `float * CBigInt`, `CBigInt * float`
- `/` : `CBigInt / CBigInt`, `float / CBigInt`, `CBigInt / float`
- 비교: `<, >, <=, >=` (CompareTo 기반)


> `CBigInt + CBigInt`는 이 스킬 범위에 포함하지 않는다(추가 설계).


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


            // source-compatible: CBigInt + float only
            var plusSmall = gold + 123f;


            Console.WriteLine($"remain: {remain}");
            Console.WriteLine($"plusSmall: {plusSmall}");
        }
    }
}
```
