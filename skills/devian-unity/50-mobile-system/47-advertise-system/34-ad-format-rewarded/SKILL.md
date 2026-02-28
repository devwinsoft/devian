# 34-ad-format-rewarded


Rewarded 광고 포맷 규약을 정의한다.


---


## Rules

- `Format=REWARDED`
- `RewardGroupId`는 필수다
- 사용자의 명시적 opt-in으로만 표시한다
- 보상 지급은 `reward earned` 콜백 이후에만 수행한다
- 동일 show cycle에서 보상은 최대 1회만 지급한다


---


## 지급 경계

- Rewarded 광고는 보상 적용을 직접 수행하지 않는다
- AdManager가 `RewardManager.ApplyRewardGroupId(rewardGroupId)`를 호출해 지급을 위임한다
- RewardManager가 `TB_REWARD`를 기준으로 `rewardGroupId`를 해석한다
- `close without reward`, `show fail`, `load fail` 경로에서는 지급이 없다


---


## Non-goals

- RewardData 직접 계산


---


## Related

- SSV 서버 검증/감사 로그: [40-ad-ssv-firebase](../40-ad-ssv-firebase/SKILL.md)
- SSV 고정 결정값: [41-ad-ssv-decisions](../41-ad-ssv-decisions/SKILL.md)
