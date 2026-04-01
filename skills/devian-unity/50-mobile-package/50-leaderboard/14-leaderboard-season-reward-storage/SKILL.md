# 14-leaderboard-season-reward-storage

Status: ACTIVE
AppliesTo: v10
Type: Design / Storage SSOT

`LeaderboardSeasonRewardStorage` 저장 모델과 SaveData codec 정본이다.

---

## Implementation Location (3-path mirror)

- UPM (정본):
  - `framework-cs/upm/com.devian.foundation/Samples~/MobilePackage/Runtime/Leaderboard/LeaderboardSeasonRewardStorage.cs`
  - `framework-cs/upm/com.devian.foundation/Samples~/MobilePackage/Runtime/SaveData/JsonCodec/SaveDataJsonCodecLeaderboardReward.cs`
- Packages (sync):
  - `framework-cs/apps/UnityExample/Packages/com.devian.foundation/Samples~/MobilePackage/Runtime/Leaderboard/LeaderboardSeasonRewardStorage.cs`
  - `framework-cs/apps/UnityExample/Packages/com.devian.foundation/Samples~/MobilePackage/Runtime/SaveData/JsonCodec/SaveDataJsonCodecLeaderboardReward.cs`
- Assets/Samples (import):
  - `framework-cs/apps/UnityExample/Assets/Samples/Devian Samples/{version}/MobilePackage/Runtime/Leaderboard/LeaderboardSeasonRewardStorage.cs`
  - `framework-cs/apps/UnityExample/Assets/Samples/Devian Samples/{version}/MobilePackage/Runtime/SaveData/JsonCodec/SaveDataJsonCodecLeaderboardReward.cs`
- SaveData 통합:
  - `.../Runtime/SaveData/JsonCodec/SaveDataJsonCodec.cs`
  - `.../Runtime/SaveData/SaveDataManager.cs`

---

## Ownership

- `LeaderboardManager`가 storage를 소유한다.
- 정본 접근 경계: `LeaderboardManager.Storage`

---

## Storage Model

`LeaderboardSeasonRewardStorage` 필드:
- `schemaVersion`
- `processedClaims: Dictionary<string, LeaderboardSeasonRewardClaimRecord>`

`LeaderboardSeasonRewardClaimRecord` 필드:
- `resultType: LeaderboardSeasonRewardResultType`
- `rank: long`
- `score: long`
- `reward_group_id: string`
- `evaluatedAtServerUtcMs: long`

`LeaderboardSeasonRewardResultType`:
- `NONE`
- `CLAIMED`
- `NO_PARTICIPATION`
- `RANK_OUT_OF_REWARD`

---

## Key Format

- claim key: `{leaderboard_id}`
- 예: `leaderboard_001`

---

## SaveData Payload

- payload key: `leaderboardReward`
- version: `v14+`

```json
{
  "version": 14,
  "leaderboardReward": {
    "schemaVersion": 1,
    "processedClaims": {
      "leaderboard_001": {
        "resultType": 1,
        "rank": 77,
        "score": 123456,
        "reward_group_id": "lb_s2026_q1_normal_51_100",
        "evaluatedAtServerUtcMs": 1760000000000
      }
    }
  }
}
```

---

## Deserialize Rules

- `storage.Clear()` 후 복원
- invalid/unknown `resultType`는 `NONE`으로 보정
- `version < 14`면 `leaderboardReward`는 clear 상태 유지

---

## Hard Rules

- 지급 여부 dedupe는 `processedClaims` 존재 여부로만 판정
- claim payload에 보상 상세(`RewardData[]`)를 저장하지 않는다
- 보상 실행 입력은 `reward_group_id`만 보존한다

---

## Related

- [03-ssot](../03-ssot/SKILL.md)
- [10-leaderboard-manager](../10-leaderboard-manager/SKILL.md)
- [21-savedata-system/43-savedata-json-codec](../../21-savedata-system/43-savedata-json-codec/SKILL.md)
