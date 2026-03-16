# devian-examples — Policy

Status: ACTIVE  
AppliesTo: v10

## SSOT

이 문서는 **Devian 예제(Examples)** 도메인의 정책 엔트리다.

예제의 기준 도메인은 **DomainKey = `Game`** 이다.

---

## 목표

- Devian 프레임워크 사용법을 예제 기반으로 설명한다.
- 예제 입력(테이블/컨트랙트/프로토콜)의 위치를 명확히 정의한다.
- 관련 스킬 문서를 한 곳에서 연결해 "따라가면 되는 길"을 제공한다.

---

## 예제 입력 위치 (SSOT)

**DomainKey = `Game`** 예제의 입력 파일:

| 입력 유형 | 경로 |
|-----------|------|
| Contracts | `devian/input/Domains/Game/*.json` |
| Tables | `devian/input/Domains/Game/*.xlsx` |
| Protocols | `devian/input/Protocols/Game/**` |

> **Note:** 위 경로의 파일들은 빌드 시 `com.devian.foundation/Samples~/GamePackage/` (Game 도메인), `com.devian.foundation/Samples~/GameProtocol/` (Game 프로토콜) 등으로 생성된다.

---

## 관련 스킬 링크 (따라가면 되는 길)

### Domain 공통 규약

도메인 샘플(`com.devian.foundation/Samples~/{DomainKey}Package/`)의 공통 규약:

- `skills/devian/20-domain-common/02-module-policy/SKILL.md`

### UPM 번들/복사 흐름

UPM 패키지가 staging → upm → Packages로 복사되는 개념:

- `skills/devian-unity/02-unity-bundles/SKILL.md`

---

## 예제 세부 문서

| 스킬 | 설명 |
|------|------|
| `skills/devian/21-domain-game/11-game-tables/SKILL.md` | Game 도메인 테이블 정의 |
| `skills/devian/30-protocol-game/SKILL.md` | Game 프로토콜(C2Game/Game2C) 예제 |

---

## 금지

- 예제 입력 파일의 스키마/내용 변경 금지 (이 문서는 "안내"만 담당)
- 새로운 정책/규칙 추가 금지 (기존 스킬/빌더 흐름을 연결만 한다)

---

## Reference

- Related: `skills/devian/10-module/03-ssot/SKILL.md`
- Related: `skills/devian/20-domain-common/02-module-policy/SKILL.md`
- Related: `skills/devian-unity/02-unity-bundles/SKILL.md`
- Related: `skills/devian/80-tools/11-builder/33-protocol-spec/SKILL.md`
