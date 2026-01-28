# 16-sound-tables

Status: ACTIVE
AppliesTo: v10
Type: Data

## Purpose

TB_SOUND / TB_VOICE 테이블의 책임 분리와 컬럼 규약을 고정한다.

---

## Hard Rules

### TB_SOUND

- TB_SOUND는 **"재생 단위(실제 AudioClip)"의 정본**이며, `bundle_key` 컬럼이 반드시 존재한다.
- 모든 사운드 재생은 `sound_id`를 통해 TB_SOUND를 참조한다.

### TB_VOICE

- TB_VOICE는 **"voice_id 중심의 논리 정의 + 모든 지원 언어 clip 매핑"**을 단일 테이블에 가진다.
- TB_VOICE의 언어별 clip 값은 **파일 경로/파일명 대신 `TB_SOUND.sound_id`를 사용**한다.
  - voice → sound 정책 일관성 유지
- **TB_VOICE는 언어별로 테이블을 쪼개지 않는다.**
  - 예: `VOICE_ko`, `VOICE_en` 같은 키/테이블 분리 금지

### 언어 컬럼 명명 규칙

- 언어별 컬럼명은 Unity `SystemLanguage.ToString()` 결과와 동일한 문자열을 사용한다.
- 예: `SystemLanguage.Korean.ToString()` == `"Korean"` → `clip_Korean`

---

## Tables

### TB_SOUND 컬럼

**필수 컬럼:**

| 컬럼명 | 타입 | 설명 |
|--------|------|------|
| `sound_id` | string | PK, 사운드 식별자 |
| `row_id` | int | 내부 row 식별자 |
| `key` | string | 게임 로딩 그룹 (레벨/씬/컨텍스트) |
| `source` | enum | 로딩 소스 (Bundle, Resources, etc.) |
| `bundle_key` | string | 번들 로드/언로드 단위 |
| `path` | string | 에셋 경로 |
| `channel` | string | 재생 채널 (BGM, SFX, Voice, etc.) |
| `loop` | bool | 루프 여부 |
| `cooltime` | float | 재생 쿨타임 (초) |
| `is3d` | bool | 3D 사운드 여부 |
| `area_close` | float | 3D 근거리 |
| `area_far` | float | 3D 원거리 |

**선택 컬럼:**

| 컬럼명 | 타입 | 설명 |
|--------|------|------|
| `weight` | int | 랜덤 선택 가중치 |
| `volume_scale` | float | 볼륨 스케일 |
| `pitch_min` | float | 피치 최소 |
| `pitch_max` | float | 피치 최대 |

### TB_VOICE 컬럼

**필수 컬럼:**

| 컬럼명 | 타입 | 설명 |
|--------|------|------|
| `voice_id` | string | PK, 보이스 식별자 |
| `text_l10n_key` | string | 자막용 StringTable 키 |

**선택 컬럼:**

| 컬럼명 | 타입 | 설명 |
|--------|------|------|
| `speaker` | string | 화자 식별자 |
| `category` | string | 카테고리 |
| `priority` | int | 재생 우선순위 |
| `group_key` | string | 그룹 키 |
| `cooltime` | float | 재생 쿨타임 |

**언어별 컬럼:**

| 컬럼명 패턴 | 타입 | 설명 |
|-------------|------|------|
| `clip_Korean` | string | 한국어 → TB_SOUND.sound_id |
| `clip_English` | string | 영어 → TB_SOUND.sound_id |
| `clip_Japanese` | string | 일본어 → TB_SOUND.sound_id |
| `clip_{SystemLanguageName}` | string | 기타 언어 → TB_SOUND.sound_id |

---

## Validation

### TB_VOICE → TB_SOUND Resolve 검증

- TB_VOICE에 존재하는 `clip_*` 값은 반드시 `TB_SOUND.sound_id`로 resolve 가능해야 한다.
- 없으면 로드 단계에서 **경고 로그 + 스킵** (기본 정책)
- 프로젝트 설정에 따라 **빌드 실패** 정책으로 전환 가능

---

## See Also

- `skills/devian-unity/30-unity-components/17-sound-manager/SKILL.md` — SoundManager 규약
- `skills/devian-unity/30-unity-components/18-voice-table-resolve/SKILL.md` — Voice Resolve 규약
