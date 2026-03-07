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
| `PurchaseTable.xlsx` | PRODUCT | TB_PRODUCT | `InternalProductId` (string) | 상품 테이블 |
| `AdvertiseTable.xlsx` | ADVERTISE | TB_ADVERTISE | `AdvertiseId` (string) | 광고 placement 테이블 |
| `RewardTable.xlsx` | REWARD | TB_REWARD | `RewardNum` (int) | 보상 테이블 |
| `GameTable.xlsx` | MESSAGE | TB_MESSAGE | `MessageId` (string) | 메시지 stat 정의 테이블 |
| `GameTable.xlsx` | MISSION_DAY | TB_MISSION_DAY | `MissionId` (string) | 일일 미션 |
| `GameTable.xlsx` | ACHIEVE | TB_ACHIEVE | `Index` (int) | 업적 런타임 + 플랫폼 매핑 테이블 |
| `GameTable.xlsx` | LEADERBOARD | TB_LEADERBOARD | `LeaderboardId` (string) | 리더보드 정의 |
| `ItemTable.xlsx` | EQUIP | TB_EQUIP | `EquipId` (string) | 장비 테이블 (EquipId, NameId, DescId) |
| `ItemTable.xlsx` | CARD | TB_CARD | `CardId` (string) | 카드 테이블 (CardId, NameId, DescId) |

### Contracts (`input/Domains/Game/`)

| 파일 | 생성 타입 | 설명 |
|---|---|---|
| `ENUM_TYPES.json` | `enum CURRENCY_TYPE`, `enum REWARD_TYPE`, `enum ADVERTISE_FORMAT`, `enum ADVERTISE_PROVIDER`, `enum STAT_TYPE` | 공통 game enum 통합 |
| `ENUM_GAME.json` | `enum GAME_MESSAGE_TYPE`, `enum GAME_MESSAGE_SAVE_TYPE` | game message 전용 enum |
| `ENUM_MISSION.json` | `enum MISSION_TYPE`, `enum MISSION_MESSAGE`, `enum ACHIEVE_MESSAGE` | mission/achieve 전용 enum |
| `ProductKind.json` | `enum ProductKind` | 상품 유형 (Consumable, Subscription, SeasonPass) |
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
| [30-ad-manager](../../50-mobile-system/47-advertise-system/30-ad-manager/SKILL.md) | AdManager | 광고 (TB_ADVERTISE 직접 참조) |
| [30-samples-purchase-manager](../../50-mobile-system/30-purchase-system/30-samples-purchase-manager/SKILL.md) | PurchaseManager | 구매 (TB_PRODUCT 직접 참조) |
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
