# 11-game-domain

Status: ACTIVE
AppliesTo: v10

**Game 도메인 허브.** DomainKey = `Game`에 관련된 모든 스킬/파일을 한 곳에서 탐색한다.

---

## 1. Overview

Game 도메인은 Devian 프레임워크의 **예제 도메인**이다.
테이블(XLSX), 컨트랙트(JSON), 프로토콜을 포함하며,
빌드 파이프라인을 통해 C#/TS/Unity 생성물을 만든다.

- **DomainKey:** `Game`
- **Namespace:** `Devian.Domain.Game` (C#), `@devian/module-game` (TS)
- **UPM:** `com.devian.domain.game`

---

## 2. Input JSON (`{buildInputJson}` 예시)

`domains.Game` 설정:

```json
"Game": {
  "contractDir": "Domains/Game",
  "contractFiles": ["*.json"],
  "tableDir": "Domains/Game",
  "tableFiles": ["*.xlsx"]
}
```

| 필드 | 값 | 설명 |
|---|---|---|
| `contractDir` | `Domains/Game` | 컨트랙트 JSON 디렉토리 |
| `contractFiles` | `["*.json"]` | 컨트랙트 파일 패턴 |
| `tableDir` | `Domains/Game` | 테이블 XLSX 디렉토리 |
| `tableFiles` | `["*.xlsx"]` | 테이블 파일 패턴 |

---

## 3. Input Files

### Tables (`input/Domains/Game/`)

| 파일 | 시트(테이블) | 컨테이너 | PK | 설명 |
|---|---|---|---|---|
| `ItemTable.xlsx` | PURCHASE | TB_PURCHASE | `InternalProductId` (string) | 상품 테이블 (`seasonId` 포함) |
| `MetaTable.xlsx` | ADVERTISE | TB_ADVERTISE | `AdvertiseId` (string) | 광고 placement 테이블 |
| `ItemTable.xlsx` | REWARD | TB_REWARD | `RewardNum` (int) | 보상 테이블 |
| `MetaTable.xlsx` | GAME_MESSAGE | TB_GAME_MESSAGE | `MessageId` (string) | 메시지 stat 정의 테이블 |
| `MetaTable.xlsx` | MISSION_DAILY | TB_MISSION_DAILY | `MissionId` (string) | 일일 미션 정의 테이블 |
| `MetaTable.xlsx` | MISSION_PERIOD | TB_MISSION_PERIOD | `MissionId` (string) | 기간 미션 정의 테이블 (`day:1~7`, 10일 주기) |
| `MetaTable.xlsx` | ACHIEVE_ONCE | TB_ACHIEVE_ONCE | `Index` (int) | 일반 업적 단계/플랫폼 매핑 테이블 |
| `MetaTable.xlsx` | ACHIEVE_PASS | TB_ACHIEVE_PASS | `Index` (int) | 패스 업적 단계/활성 조건 테이블 |
| `MetaTable.xlsx` | LEADERBOARD | TB_LEADERBOARD | `LeaderboardId` (string) | 리더보드 정의 |
| `MetaTable.xlsx` | LEADERBOARD_REWARD | TB_LEADERBOARD_REWARD | `Index` (int) | 리더보드 구간 보상 정의 |
| `ItemTable.xlsx` | ITEM_EQUIP | TB_ITEM_EQUIP | `EquipId` (string) | 장비 테이블 (EquipId, NameId, DescId) |
| `ItemTable.xlsx` | ITEM_CARD | TB_ITEM_CARD | `CardId` (string) | 카드 테이블 (CardId, NameId, DescId) |
| `ItemTable.xlsx` | ITEM_RENTAL | TB_ITEM_RENTAL | `RentalId` (string) | 렌탈 아이템 테이블 (RentalId, NameId, DescId) |
| `ItemTable.xlsx` | ITEM_PASS | TB_ITEM_PASS | `PassId` (string) | 패스 아이템 테이블 (PassId, NameId, DescId) |
| `ItemTable.xlsx` | SEASON | TB_SEASON | `SeasonId` (string) | 시즌 메타 테이블 (start/end UTC) |

### Contracts (`input/Domains/Game/`)

| 파일 | 생성 타입 | 설명 |
|---|---|---|
| `ENUM_GAME.json` | `enum STAT_TYPE`, `enum GAME_MESSAGE_TYPE`, `enum GAME_MESSAGE_SAVE_TYPE`, `enum GAME_MESSAGE_OP_TYPE` | game/message 공통 enum |
| `ENUM_META.json` | `enum LEADERBOARD_MODE`, `enum MISSION_TYPE(DAILY/PERIOD)`, `enum MESSAGE_MISSION_TYPE`, `enum MESSAGE_ACHIEVE_TYPE`, `enum ACHIEVE_TYPE`, `enum CURRENCY_TYPE`, `enum REWARD_TYPE`, `enum ADVERTISE_FORMAT`, `enum ADVERTISE_PROVIDER`, `enum PURCHASE_KIND`, `enum MESSAGE_INVENTORY_TYPE` | 메타 시스템 enum 통합 |
| `TestContract.json` | `enum UserType`, `class UserProfile` | 테스트 예제 (UserType: Guest/Member/Admin, UserProfile: Id/Name/UserType) |

---

## 4. Generated Outputs

| 플랫폼 | 생성물 | 경로 |
|---|---|---|
| C# Module | `Devian.Domain.Game` | `framework-cs/module/Devian.Domain.Game/` |
| UPM Package | `com.devian.domain.game` | `framework-cs/upm/com.devian.domain.game/` |
| TS Module | `devian-domain-game` | `framework-ts/module/devian-domain-game/` |
| Data (ndjson) | `*.json` | `{tableConfig.tableDirs}/ndjson/` |
| Data (pb64) | `*.asset` | `{tableConfig.tableDirs}/pb64/` |

빌드 실행: `bash input/build.sh {buildInputJson}` (예: `bash input/build.sh input/build_input.json`)

---

## 5. Game Contents (Unity Samples)

Game 도메인을 사용하는 스킬:

### GameContents (`com.devian.samples/Samples~/GameContents`)

| 스킬 | 핵심 타입 | 설명 |
|---|---|---|
| [12-game-ability](../12-game-ability/SKILL.md) | AbilityBase, AbilityEquip, AbilityCard, STAT_TYPE | 능력치 정규화 시스템 |

### MobileSystem (`com.devian.samples/Samples~/MobileSystem`)
| [10-inventory-manager](../../50-mobile-system/22-inventory-system/10-inventory-manager/SKILL.md) | InventoryManager | 인벤토리 + InventoryStorage |
| [11-inventory-storage](../../50-mobile-system/22-inventory-system/11-inventory-storage/SKILL.md) | InventoryStorage | 인벤토리 데이터 컨테이너 |
| [30-ads-manager](../../50-mobile-system/47-advertise-system/30-ads-manager/SKILL.md) | AdsManager | 광고 (TB_ADVERTISE 직접 참조) |
| [30-samples-purchase-manager](../../50-mobile-system/30-purchase-system/30-samples-purchase-manager/SKILL.md) | PurchaseManager | 구매 (TB_PURCHASE 직접 참조) |
| [10-reward-manager](../../50-mobile-system/49-reward-system/10-reward-manager/SKILL.md) | RewardManager | 보상 (TB_REWARD 직접 참조) |

개요: [21-domain-game-system/00-overview](../00-overview/SKILL.md)

---

## 6. Protocol

| 프로토콜 | 방향 | 파일 |
|---|---|---|
| `C2Game` | Client → Server | `input/Protocols/Game/C2Game.json` |
| `Game2C` | Server → Client | `input/Protocols/Game/Game2C.json` |

- **ProtocolGroup:** `Game`
- **C# Namespace:** `Devian.Protocol.Game`
- **UPM:** `com.devian.protocol.game`
- **TS:** `@devian/protocol-game`

상세: [14-game-protocol](../14-game-protocol/SKILL.md)

---

## 7. Related

| 주제 | 스킬 |
|---|---|
| STAT_TYPE enum 값 관리 | [13-game-stat-type](../13-game-stat-type/SKILL.md) |
| Game 프로토콜 예제 | [14-game-protocol](../14-game-protocol/SKILL.md) |
| Examples SSOT (config/input) | [devian-examples/03-ssot](../../../devian-examples/03-ssot/SKILL.md) |
| Builder SSOT (테이블/컨트랙트 규칙) | [devian/80-tools/11-builder/03-ssot](../../../devian/80-tools/11-builder/03-ssot/SKILL.md) |
| Root SSOT (용어/경로) | [devian/10-module/03-ssot](../../../devian/10-module/03-ssot/SKILL.md) |
| 21-domain-game-system 개요 | [00-overview](../00-overview/SKILL.md) |
| MobileSystem 개요 | [50-mobile-system/00-overview](../../50-mobile-system/00-overview/SKILL.md) |
