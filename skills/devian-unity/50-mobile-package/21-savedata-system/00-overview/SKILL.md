# 21-savedata-system — Overview


Status: ACTIVE
AppliesTo: v10


SaveData System은 로컬/클라우드 저장 및 동기화를 위한 스킬 그룹이다.
SaveDataManager가 유일한 진입점이며, SaveLocal/SaveCloud 로직을 단일 매니저로 통합한다.


---


## Start Here


| Document | Description |
|----------|-------------|
| [10-savedata-manager](../10-savedata-manager/SKILL.md) | SaveDataManager 설계(단일 진입점) |
| [11-savedata-settings](../11-savedata-settings/SKILL.md) | SaveDataSettings (설정 ScriptableObject) |
| [41-savedata-savelocal](../41-savedata-savelocal/SKILL.md) | SaveLocal(Internal) |
| [42-savedata-savecloud](../42-savedata-savecloud/SKILL.md) | SaveCloud(Internal) |
| [43-savedata-json-codec](../43-savedata-json-codec/SKILL.md) | SaveData JSON 직렬화/역직렬화 규약 |


---


## Related

- [20-account-system](../../20-account-system/00-overview/SKILL.md)
- [48-mission-system](../../48-mission-system/00-overview/SKILL.md)
- [49-reward-system](../../49-reward-system/00-overview/SKILL.md)
- [22-inventory-system](../../22-inventory-system/00-overview/SKILL.md)
