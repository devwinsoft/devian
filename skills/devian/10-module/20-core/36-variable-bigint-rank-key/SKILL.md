# Devian v11 — Feature: CBigInt.RankKey

Status: ACTIVE
AppliesTo: v10

## Purpose

CBigInt → long rank key 변환. 플랫폼 리더보드(GPGS/Apple)에 long 하나로 점수를 전달할 때 사용한다.

핵심: **정렬 순서 보존**이 목적이다. 실제 값 복원 목적이 아니다.

---

## 설계 원칙

1. **순서 보존**: `a.CompareTo(b)` 결과와 `a.RankKey.CompareTo(b.RankKey)` 결과가 항상 동일해야 한다.
2. **정규화 전제**: 인코딩 전 반드시 정규화 상태를 보장한다 (`1 <= abs(mBase) < 10`).
3. **부호 구간 분리**: `negative key < 0 = zero key < positive key`.
4. **오버플로 보호**: mPow가 허용 범위를 벗어나면 clamp 처리한다.

---

## 인코딩 구조 (packed long)

CBigInt의 비교 규칙(sign → mPow → mBase)을 그대로 long에 packing한다.

```
key = sign × (1 + biasedPow × MANTISSA_SCALE + mantissaBucket)
```

### 구성 요소

| 요소 | 공식 | 범위 |
|------|------|------|
| `mantissaBucket` | `floor((abs(mBase) - 1) × MANTISSA_PRECISION)` | 0 ~ 8,999,999 |
| `biasedPow` | `mPow + POW_BIAS` | 0 ~ 2 × POW_BIAS |
| `magnitudeKey` | `biasedPow × MANTISSA_SCALE + mantissaBucket` | 0 ~ max |

### 부호별 변환

| 부호 | key | 보장 |
|------|-----|------|
| 양수 | `+(1 + magnitudeKey)` | always >= 1 |
| 0 | `0` | 고정 |
| 음수 | `-(1 + magnitudeKey)` | always <= -1 |

음수 구간에서 절대값이 클수록 magnitudeKey가 크고, 부호 반전으로 key가 더 작아진다.
따라서 -100 < -10 정렬이 보존된다.

---

## 인코딩 상수

| 상수 | 값 | 근거 |
|------|-----|------|
| `MantissaPrecision` | 1,000,000 | 소수점 이하 6자리 정밀도 |
| `MantissaScale` | 10,000,000 | mantissaBucket 최대값(8,999,999)보다 큰 10^7 |
| `PowBias` | 1,000,000 | mPow 범위 ±100만 커버 (게임 경제 충분) |

### 오버플로 검증

- max magnitudeKey = 2,000,000 × 10,000,000 + 8,999,999 ≈ 2 × 10^13
- max key = 1 + 2 × 10^13 ≈ 2 × 10^13
- `long.MaxValue` ≈ 9.2 × 10^18 → **충분한 여유**
- mPow가 ±PowBias 범위를 벗어나면 clamp (`long.MaxValue` / `long.MinValue`)

---

## Public API

```csharp
// CBigInt struct 내부
public long RankKey { get; }
public static CBigInt FromRankKey(long rankKey);
```

### RankKey (CBigInt → long)
- 0이면 `0L` 반환
- 정규화 후 인코딩 (private `NormalizeRaw` 재활용)
- mPow 범위 초과 시 양수면 `long.MaxValue`, 음수면 `long.MinValue`로 clamp

### FromRankKey (long → CBigInt)
- `0` → `CBigInt.Zero`
- 양수: `magnitudeKey = rankKey - 1` → biasedPow/mantissaBucket 분리 → mPow/mBase 복원
- 음수: `magnitudeKey = -(rankKey + 1)` → 동일 역변환 후 mBase 부호 반전
- 정밀도: mantissa 6자리 (float ~7자리와 거의 동일)

---

## NDJSON/pb64 저장

`class:CBigInt` 타입의 테이블 필드는 NDJSON/pb64에 **rankKey (long)** 값으로 저장된다.

- 빌드 시: XLSX 셀 → base/pow 파싱 → `computeCBigIntRankKey()` → long 저장
- 로드 시: long → `CBigInt.FromRankKey(long)` → CBigInt 복원
- `CBigIntRankKeyConverter` (JsonConverter)가 자동 변환 처리

---

## 테스트 케이스 (필수)

| 카테고리 | 케이스 |
|----------|--------|
| 기본 비교 | 양수끼리 mPow 다름 / 양수끼리 mPow 같고 mBase 다름 |
| 음수 정렬 | 절대값 큰 음수가 더 작은 key |
| 부호 경계 | negative < zero < positive |
| 경계값 | `0`, `{1,0}`, `{9.999,0}`, `{1,-1}`, `{9.9,999999}` |
| 정규화 동치 | `{55,5}` == `{5.5,6}` → 동일 key |
| 오버플로 | mPow > PowBias → `long.MaxValue` |
| 랜덤 샘플 | 수천 쌍 생성 후 `CompareTo` vs `RankKey` 비교 일치 검증 |

---

## Related

- [35-variable-bigint](../35-variable-bigint/SKILL.md) — CBigInt 본체
- Implementation: `framework-cs/module/Devian/src/Variable/CBigInt.cs`
