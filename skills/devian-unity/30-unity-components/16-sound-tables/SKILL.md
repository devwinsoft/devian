# 16-sound-tables

Status: ACTIVE
AppliesTo: v10
Type: Data

## Purpose

TB_SOUND / TB_VOICE 테이블의 책임 분리와 컬럼 규약을 고정한다.

---

## Domain Ownership (Important)

### 테이블 소유 도메인

- **최종적으로 TB_SOUND, TB_VOICE는 Sound 도메인 소유**이다.
- 네임스페이스 목표: `Devian.Domain.Sound`

### 현재 상태 (Sound 도메인 완료)

| 항목 | 상태 |
|------|------|
| 데이터 파일 | `input/Domains/Sound/tables/SoundTable.xlsx` |
| Generated 위치 | `com.devian.domain.sound` |
| 네임스페이스 | `Devian.Domain.Sound` |

> 도메인 설계는 `19-sound-domain/SKILL.md` 참조.

---

## Hard Rules

### partial class 패턴 (Generated + 확장)

- TB_SOUND, TB_VOICE 컨테이너는 **Generated 코드에서 `partial class`로 선언**된다.
- 수기 유지 파일(TB_SOUND.cs, TB_VOICE.cs)은 **반드시 `partial` 키워드를 사용**하여 확장한다.
- Generated 클래스(SOUND, VOICE)는 PascalCase 프로퍼티를 사용하며, ISoundRow/IVoiceRow 인터페이스는 snake_case를 사용한다.
- **Adapter 패턴**으로 Generated 클래스를 인터페이스에 맞춘다: `SoundRowAdapter`, `VoiceRowAdapter`

### TB_SOUND

- TB_SOUND는 **"재생 단위(실제 AudioClip)"의 정본**이며, `key_bundle` 컬럼이 반드시 존재한다.
- **`row_id`가 PK**(내부 row 식별자, 재생 단위)이며, `sound_id`는 **논리 그룹 키**(중복 허용)이다.
- 동일 `sound_id`에 여러 row가 존재할 수 있다 (weight 기반 랜덤 선택 지원).
- 모든 사운드 재생은 `sound_id`를 통해 TB_SOUND를 참조하며, 해당 sound_id의 후보 row 중 weight 기반으로 1개를 선택한다.
- `weight` 값이 0 이하인 경우 1로 취급한다.

### TB_VOICE (SOUND 미참조, 독립 운용)

- TB_VOICE는 **"voice_id 중심의 논리 정의 + 모든 지원 언어 clip 매핑"**을 단일 테이블에 가진다.
- **TB_VOICE의 언어별 clip_* 값은 AudioClip 에셋 경로/키(clipPath)를 직접 사용한다.**
  - VOICE는 SOUND 테이블을 참조하지 않는다 (sound_id 매핑/연동 금지)
  - clip_* 값은 더 이상 sound_id가 아니라 **clipPath(AudioClip 에셋 경로/키)**이다.
  - clip 경로는 IVoiceRow.TryGetClipColumn()으로 직접 조회
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
| `row_id` | int | **PK**, 내부 row 식별자 (재생 단위) |
| `sound_id` | string | 논리 사운드 식별자 (그룹 키, 중복 허용) |
| `isBundle` | bool | 로딩 소스 (true=Bundle, false=Resources.Load) |
| `key_bundle` | string | **로드/언로드 단위 키** (Bundle label 또는 Resource 그룹 키) |
| `path` | string | 에셋 경로 |
| `channel` | SoundChannelType | 재생 채널 (enum: Bgm, Effect, Ui, Voice) |
| `loop` | bool | 루프 여부 |
| `cooltime` | float | 재생 쿨타임 (초) |
| `is3d` | bool | 3D 사운드 여부 |
| `distance_near` | float | 3D near 거리 (minDistance), 기본값 1.0 |
| `distance_far` | float | 3D far 거리 (maxDistance), 기본값 500.0 |
| `weight` | int | 랜덤 선택 가중치, 기본값 1 |
| `volume_scale` | float | 볼륨 스케일, 기본값 1.0 |
| `pitch_min` | float | 피치 랜덤 최소, 기본값 1.0 |
| `pitch_max` | float | 피치 랜덤 최대, 기본값 1.0 |

**key_bundle 규약 (Hard Rule):**
- **로드/언로드는 `key_bundle` 단위로만 수행**한다 (key_group 제거됨).
- `isBundle=true`: Addressables label/key로 사용
- `isBundle=false`: Resources 폴더 그룹 키로 사용 (AssetManager Resource API 기반 로드/언로드)
- isBundle=false인 행도 반드시 key_bundle을 채워야 한다.

**컬럼명 변경 히스토리:**
- `key` → ~~`key_group`~~ → **삭제** (key_bundle로 통합)
- `source` → `isBundle` (enum → bool 단순화)
- `bundle_key` → `key_bundle` (접두어 통일)
- `channel` → SoundChannelType enum (string → enum)

**weight 규약 (SOUND 전용):**
- `weight`는 **SOUND 테이블 전용** 필드이다.
- 동일 sound_id 후보 rows 중 가중치 기반 랜덤 선택에 사용된다.
- VOICE 테이블에는 weight 필드가 없으며 사용하지 않는다.

> **3D 파라미터**: `distance_near`/`distance_far`는 BaseAudioManager의 Play3D에서 직접 사용된다.
> 자세한 내용은 `20-base-audio-manager/SKILL.md` 참조.

### TB_VOICE 컬럼

**필수 컬럼:**

| 컬럼명 | 타입 | 설명 |
|--------|------|------|
| `voice_id` | string | PK, 보이스 식별자 |
| `key_bundle` | string | **로드/언로드 단위 키** (Voice 전용 번들) |
| `cooltime` | float | 재생 쿨타임 |
| `is3d` | bool | 3D 여부 |
| `distance_near` | float | 3D near 거리 (minDistance), 기본값 1.0 |
| `distance_far` | float | 3D far 거리 (maxDistance), 기본값 500.0 |
| `volume_scale` | float | 볼륨 스케일, 기본값 1.0 |
| `pitch_min` | float | 피치 랜덤 최소, 기본값 1.0 |
| `pitch_max` | float | 피치 랜덤 최대, 기본값 1.0 |

**제거된 컬럼:**
- `speaker`, `category`, `priority` — 삭제됨 (게임 로직 메타이므로 오디오 테이블 범위 밖)
- `key_group` — 삭제됨 (key_bundle로 통합)

**언어별 컬럼:**

| 컬럼명 패턴 | 타입 | 설명 |
|-------------|------|------|
| `clip_Korean` | string | 한국어 clip 파일 경로 |
| `clip_English` | string | 영어 clip 파일 경로 |
| `clip_Japanese` | string | 일본어 clip 파일 경로 |
| `clip_Chinese` | string | 중국어 clip 파일 경로 (간체/번체 통합) |
| `clip_{SystemLanguageName}` | string | 기타 언어 clip 파일 경로 |

> **중국어 통합**: `SystemLanguage.Chinese`를 사용하며, 간체/번체 구분 없이 `clip_Chinese` 단일 컬럼으로 관리한다.

> **clip_* 값의 의미**: clip_* 컬럼 값은 **AudioClip 에셋 경로/키(clipPath)**이다.
> 더 이상 sound_id가 아니며, VOICE는 SOUND 테이블을 참조하지 않는다.

**VOICE 상수 (코드 하드코딩):**
- `isBundle = true` (항상 Bundle)
- `channel = SoundChannelType.Voice` (항상 Voice)
- `loop = false` (항상 비루프)
- `volume_scale = 1f`
- `pitch_min = 1f`, `pitch_max = 1f`

---

## Row4 주석 규약 (Excel 시트)

Excel 테이블의 Row4(주석 행)에 아래 정보를 반드시 기입한다:

### SOUND 시트 Row4

| 컬럼 | Row4 주석 (필수) |
|------|------------------|
| `channel` | `enum:SoundChannelType: Bgm=0, Effect=1, Ui=2, Voice=3, Max=4` |
| `isBundle` | `false=Resources.Load, true=Bundle` |

### VOICE 시트 Row4

VOICE 시트에는 기본값 규약을 Row4 주석으로 기입한다:

| 컬럼 (아무 컬럼) | Row4 주석 (필수) |
|------------------|------------------|
| `voice_id` (또는 첫 컬럼) | `VOICE defaults: isBundle=true, channel=Voice, loop=false` |

> Row4 주석은 코드 생성기/문서화 도구에서 참조할 수 있다.

---

## IAudioRowBase Interface

SOUND와 VOICE가 공통으로 구현하는 인터페이스:

```csharp
public interface IAudioRowBase
{
    string key_bundle { get; }
    bool isBundle { get; }
    SoundChannelType channel { get; }
    bool loop { get; }
    float cooltime { get; }
    bool is3d { get; }
    float distance_near { get; }
    float distance_far { get; }
    float volume_scale { get; }
    float pitch_min { get; }
    float pitch_max { get; }
}
```

> **key_group 제거**: 로드/언로드는 `key_bundle` 단위로만 수행한다.

- `ISoundRow : IAudioRowBase` — SOUND 전용 멤버 추가 (sound_id, row_id, path, weight)
- `IVoiceRow : IAudioRowBase` — VOICE 전용 멤버 추가 (voice_id, TryGetClipColumn)

---

## Validation

### TB_VOICE clip 경로 검증

- TB_VOICE에 존재하는 `clip_*` 값은 유효한 파일 경로여야 한다.
- 없으면 로드 단계에서 **경고 로그 + 스킵** (기본 정책)
- 프로젝트 설정에 따라 **빌드 실패** 정책으로 전환 가능

---

## See Also

- `skills/devian-unity/30-unity-components/19-sound-domain/SKILL.md` — **Sound 도메인 설계 (SSOT)**
- `skills/devian-unity/30-unity-components/17-sound-manager/SKILL.md` — SoundManager 규약
- `skills/devian-unity/30-unity-components/18-voice-table-resolve/SKILL.md` — Voice Resolve 규약
- `skills/devian-unity/30-unity-components/20-base-audio-manager/SKILL.md` — BaseAudioManager 공통 Play 규약
