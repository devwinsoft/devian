# 10-foundation Overview

## Purpose
Unity 런타임 컴포넌트(비-UI)의 스킬 인덱스와 진입점이다.
이 폴더는 "Unity 컴포넌트" 범위만 다루며, UI는 30-ui-system로 분리되어 있다.

## Scope

### Includes
- Unity 런타임 일반 컴포넌트(풀, 싱글톤, 메인스레드 유틸 등)
- Bootstrap/런타임 하부 구성요소(단, UI 제외)

### Excludes
- UI 관련 컴포넌트 / Canvas / UI 입력 등 → `skills/devian-unity/30-ui-system/`
- 게임/도메인 기능(프로젝트별 구성)
- 공용 이펙트/머티리얼 효과 → `skills/devian-unity/11-common-system/00-overview/SKILL.md`

## Where to start
- Index: `skills/devian-unity/10-foundation/SKILL.md`
- UI: `skills/devian-unity/30-ui-system/SKILL.md`

## Conventions
- 번호 `00-*`는 overview 전용이다.
- 이 영역의 스킬 파일명은 `SKILL.md`를 사용한다.

## Related

| Topic | Link | Notes |
| --- | --- | --- |
| CommonSystem | [11-common-system](../../11-common-system/00-overview/SKILL.md) | 공용 도메인 + 공용 런타임 기능 |
| DevianSettings | [11-devian-settings](../../11-common-system/11-devian-settings/SKILL.md) | CommonSystem 소유 설정 자산 |
