# 20-save-system — Minigame DoD (Cloud Save)


Status: ACTIVE
AppliesTo: v10


---


## Hard DoD


- 최소 2~3 씬 미니게임에서 로컬 세이브가 동작한다.
- 플랫폼이 지원/허용하는 경우 Cloud Save가 동작한다.
- 오프라인에서도 게임은 진행 가능하고, 온라인 복귀 시 업로드 재시도가 가능하다. **(서비스 레이어 책임)**
- 충돌 시 플랫폼 자동 해결이 동작한다(GPGS: `UseLongestPlaytime`).


---


## Soft DoD


- Slot 1~2(backup/manual) 지원(필요할 때만)
- checksum(최소 무결성) 지원(필요할 때만)
