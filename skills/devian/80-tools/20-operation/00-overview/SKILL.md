# 20-operation — Overview


Status: ACTIVE
AppliesTo: v10


게임 운영(Operation)을 위한 **로컬 웹앱** 스킬 그룹이다.
개발자가 게임 데이터의 encode/decode, 조회/수정을 수행하는 데 사용하는 도구들을 정의한다.

단일 페이지 내에서 **탭**으로 기능을 전환한다.


---


## Features (탭)

| 탭 | 상태 | 버튼 | 설명 |
|----|------|------|------|
| Obfuscate | 구현됨 | [Obfuscate] | 평문 -> 난독화 (byte-substitution) |
| Deobfuscate | 구현됨 | [Deobfuscate] | 난독화 -> 평문 |
| Save Data | 구현됨 | [Import] / [Export & Download] | .dvn <-> JSON 통합 워크플로우 |

> 탭 이름, 버튼 이름은 영문으로 표기한다. 탭은 필요에 따라 추가한다.


---


## Tech Stack

| 항목 | 선택 |
|------|------|
| 언어 | TypeScript |
| UI | 바닐라 TS + 경량 라이브러리 (프레임워크 없음) |
| 서버 | localhost 전용 (배포 없음) |
| 빌드 | Vite |

AES 공유키가 클라이언트 JS에 포함되므로, localhost 전용으로만 운영한다.


## Project Location

```
framework-ts/apps/Operation/
```


---


## Start Here


| Document | Description |
|----------|-------------|
| [01-policy](../01-policy/SKILL.md) | 정책 (localhost 전용, 바닐라 TS, 보안) |
| [03-ssot](../03-ssot/SKILL.md) | 기능(탭) 정의 (입출력, 파이프라인) |
| [10-app-shell](../10-app-shell/SKILL.md) | 앱 셸 (빌드, 프로젝트 구조, 탭 네비게이션) |
| [15-layout](../15-layout/SKILL.md) | 레이아웃, 스타일 가이드 |
| [16-page-obfuscate](../16-page-obfuscate/SKILL.md) | Obfuscate 탭 UI |
| [17-page-deobfuscate](../17-page-deobfuscate/SKILL.md) | Deobfuscate 탭 UI |
| [18-page-savedata](../18-page-savedata/SKILL.md) | Save Data 탭 UI |


---


## Related

- [25-recovery-system/03-ssot](../../../../devian-unity/50-mobile-system/25-recovery-system/03-ssot/SKILL.md) — DVN 포맷/AES 정본
- [80-tools Overview](../../00-overview/SKILL.md) — Tools 그룹 상위 개요
