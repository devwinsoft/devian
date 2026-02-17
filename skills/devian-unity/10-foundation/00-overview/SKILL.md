# 10-foundation Overview

## Purpose
Unity 런타임 컴포넌트(비-UI)의 스킬 인덱스와 진입점이다.
이 폴더는 "Unity 컴포넌트" 범위만 다루며, UI는 40-ui-system로 분리되어 있다.

## Scope

### Includes
- Unity 런타임 일반 컴포넌트(풀, 싱글톤, 메인스레드 유틸 등)
- Bootstrap/런타임 하부 구성요소(단, UI 제외)

### Excludes
- UI 관련 컴포넌트 / Canvas / UI 입력 등 → `skills/devian-unity/40-ui-system/`
- 게임/도메인 기능(프로젝트별 구성)

## Where to start
- Index: `skills/devian-unity/10-foundation/SKILL.md`
- UI: `skills/devian-unity/40-ui-system/SKILL.md`

## Conventions
- 번호 `00-*`는 overview 전용이다.
- 이 영역의 스킬 파일명은 `SKILL.md`를 사용한다.

## Effect System (migrated from 14-effect-system)

이 그룹에는 Effect 시스템 스킬이 통합되었다.

| Topic | Link | Notes |
| --- | --- | --- |
| Policy | [01-policy](../01-policy/SKILL.md) | Effect 시스템 정책 |
| SSOT | [27-effect-system](../27-effect-system/SKILL.md) | Effect 시스템 정본(경로/규칙/링크) |
| CommonEffectManager | [29-common-effect-manager](../29-common-effect-manager/SKILL.md) | 공통 이펙트 |
| MaterialEffectController | [28-material-effect-controller](../28-material-effect-controller/SKILL.md) | 머티리얼 스위치 |
| MaterialEffectId | [13-material-effect-id](../13-material-effect-id/SKILL.md) | MATERIAL_EFFECT_ID |
