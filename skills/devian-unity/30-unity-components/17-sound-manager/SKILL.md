# 17-sound-manager

Status: ACTIVE
AppliesTo: v10
Type: Component

## Purpose

SoundManager는 테이블 기반으로 사운드 재생/풀/채널/쿨타임을 관리한다.

---

## Architecture

| 컴포넌트 | 책임 |
|----------|------|
| **SoundManager** (전역) | 채널 생성/관리, 테이블 인덱싱, 로딩 그룹 정책 |
| **SoundChannel** (채널) | 풀/재생 리스트/쿨타임/SEQ 관리 |
| **SoundPlay** (재생 유닛) | Wait/FadeIn/Play/FadeOut 상태 처리 |

---

## Hard Rules

### 테이블 기반 식별

- SoundManager는 **테이블(TB_SOUND) 기반으로만 식별**한다.
- 직접 경로 문자열 하드코딩 금지.

### Play API

- `SoundManager.Play(...)`는 입력으로 `sound_id`를 받는다.
- 내부에서 변형 row(weight/random) 선택 후 클립을 resolve한다.

### key vs bundle_key 구분 (Hard Rule)

| 필드 | 용도 | 예시 |
|------|------|------|
| `bundle_key` | 번들 로드/언로드 단위 | `"sound_battle"`, `"sound_ui"` |
| `key` | 게임 로딩 그룹 (레벨/씬/컨텍스트) | `"GLOBAL"`, `"BATTLE"`, `"LOBBY"` |

- 둘을 혼동 금지.
- `key`는 게임 로직 단위, `bundle_key`는 에셋 번들 단위.

### 채널/풀 책임 분리

- 채널/풀/쿨타임은 **SoundChannel 책임**이다.
- SoundManager가 AudioSource를 직접 들고 운용하지 않는다.

### SerializeField 의존 최소화

- SoundManager는 **SerializeField 의존 없이 런타임 생성 원칙**을 권장한다.
- 프로젝트 정책에 따라 조정 가능하나, 스킬은 "참조 의존 최소화"로 고정.

---

## Loading Policy

### LoadByKey / UnloadByKey

```csharp
SoundManager.I.LoadByKey("GLOBAL");
SoundManager.I.UnloadByKey("BATTLE");
```

- `TB_SOUND.key`로 row를 등록/해제한다.
- `source` / `bundle_key` 규칙으로 에셋 로딩/언로딩을 수행한다.

### source == Bundle

- `bundle_key` 기준으로 로딩 단위를 잡는다.
- 구현 상세는 코드에 위임하되 규약만 고정.

---

## Usage Examples

```csharp
// 글로벌 사운드 로드
SoundManager.I.LoadByKey("GLOBAL");

// 기본 재생
SoundManager.I.Play("UI_CLICK");

// 볼륨/그룹 지정 재생
SoundManager.I.Play("SFX_HIT", volume: 0.8f, groupId: 123);

// 3D 사운드 재생
SoundManager.I.Play("SFX_EXPLOSION", position: transform.position);

// 언로드
SoundManager.I.UnloadByKey("BATTLE");
```

---

## Non-goals

- Voice 로컬라이징 규칙은 여기서 다루지 않는다.
- Voice 관련 규칙은 `18-voice-table-resolve` 스킬로 위임 (중복 방지).

---

## See Also

- `skills/devian-unity/30-unity-components/16-sound-tables/SKILL.md` — TB_SOUND/TB_VOICE 테이블 규약
- `skills/devian-unity/30-unity-components/18-voice-table-resolve/SKILL.md` — Voice Resolve 규약
