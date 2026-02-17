# 40-ui-system Overview

## Purpose
Unity UI 관련 컴포넌트/규약을 모아둔 스킬 인덱스의 진입점이다.
UIManager 및 UI Canvas/Frame 규약을 포함한다.

## Scope

### Includes
- UIManager (AutoSingleton, Canvas 수명주기)
- UI Canvas/Frame 규약 및 UI 관련 보조 컴포넌트

### Excludes
- 비-UI Unity 컴포넌트 → `skills/devian-unity/10-foundation/`
- 게임플레이 입력(ActionMap/리바인딩/컨텍스트 전환)

## Where to start
- Index: `skills/devian-unity/40-ui-system/SKILL.md`
- Non-UI: `skills/devian-unity/10-foundation/SKILL.md`

## Conventions
- 번호 `00-*`는 overview 전용이다.
- 이 영역의 스킬 파일명은 `SKILL.md`를 사용한다.
