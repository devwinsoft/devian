# 20-base-audio-manager

Status: ACTIVE
AppliesTo: v10
Type: Component

## Purpose

BaseAudioManager는 2D/3D 사운드 재생의 공통 로직을 제공한다.
SoundManager/VoiceManager는 Play 로직을 직접 구현하지 않고, BaseAudioManager에 위임한다.

---

## Hard Rules

### 공통 베이스 원칙

- **공통 베이스 이름은 `BaseAudioManager`**이다.
- SoundManager/VoiceManager는 Play 로직(AudioSource 획득, 볼륨/피치 적용, 3D 설정)을 직접 구현하지 않는다.
- 2D/3D Play, AudioSource 풀링, pitch/volume 랜덤, 3D 거리 파라미터 적용은 전부 BaseAudioManager가 담당한다.

### 3D 파라미터 SSOT

**3D 파라미터의 SSOT는 SOUND row 필드**이다. 아래 필드를 사용한다:

| 필드명 | 타입 | 설명 | 기본값 |
|--------|------|------|--------|
| `distance_near` | float | 3D near 거리 (= minDistance) | 1.0 |
| `distance_far` | float | 3D far 거리 (= maxDistance) | 500.0 |
| `volume_scale` | float | 최종 볼륨 스케일 | 1.0 |
| `pitch_min` | float | 피치 랜덤 범위 최소 | 1.0 |
| `pitch_max` | float | 피치 랜덤 범위 최대 | 1.0 |
| `weight` | int | 랜덤 선택 가중치 (복수 row 존재 시) | 1 |

**필드명 변경 히스토리:**
- `area_close` → `distance_near` (의미 명확화)
- `area_far` → `distance_far` (의미 명확화)

### Play 파라미터 적용 규칙

1. **볼륨**: `finalVolume = row.volume_scale * externalVolumeScale`
2. **피치**: `finalPitch = Random.Range(row.pitch_min, row.pitch_max)` (min == max면 고정)
3. **3D 거리**: `minDistance = row.distance_near`, `maxDistance = row.distance_far`
4. **weight 기반 랜덤 선택**: 동일 sound_id에 row가 여러 개면 weight로 row 선택

---

## API

### BaseAudioManager (abstract/static)

```csharp
// 2D Play - 위치 정보 없음
SoundRuntimeId Play2D(
    ISoundRow row,
    AudioClip clip,
    float volumeScale = 1f,
    float pitchOverride = 0f,  // 0이면 row에서 랜덤 계산
    bool loop = false,
    float cooltime = 0f,
    int groupId = 0
);

// 3D Play - 위치 정보 필수
SoundRuntimeId Play3D(
    ISoundRow row,
    AudioClip clip,
    Vector3 position,
    float volumeScale = 1f,
    float pitchOverride = 0f,
    bool loop = false,
    float cooltime = 0f,
    int groupId = 0
);
```

### SoundManager (위임)

```csharp
// 기존 API 유지
SoundRuntimeId PlaySound(string soundId, float volume = 1f, ...);

// 3D API 추가
SoundRuntimeId PlaySound3D(string soundId, Vector3 position, float volume = 1f, ...);
```

### VoiceManager (위임)

```csharp
// 기존 API 유지
SoundRuntimeId PlayVoice(string voiceId, float volume = 1f, ...);

// 3D API 추가
SoundRuntimeId PlayVoice3D(string voiceId, Vector3 position, float volume = 1f, ...);
```

---

## Architecture

```
[SoundManager]  [VoiceManager]
      |               |
      v               v
+----------------------------------+
|        BaseAudioManager          |
| - Play2D(row, clip, ...)         |
| - Play3D(row, clip, pos, ...)    |
| - AudioSource pool management    |
| - Volume/Pitch calculation       |
| - 3D distance setup              |
+----------------------------------+
           |
           v
    [SoundChannel]
    [SoundPlay]
```

---

## Implementation Notes

### BaseAudioManager 구현 방식

BaseAudioManager는 다음 중 하나로 구현 가능:

1. **Static Helper Class** (권장): SoundManager 내부에서 호출하는 static 메서드 집합
2. **Abstract Base Class**: SoundManager/VoiceManager가 상속
3. **Component Composition**: 별도 MonoBehaviour로 분리

현재 구현은 **Static Helper Class** 방식을 권장한다 (기존 구조 변경 최소화).

### 중복 로직 제거 대상

SoundManager/VoiceManager에서 제거해야 하는 중복 로직:
- 볼륨/피치 계산 (`volume * row.volume_scale`, `Random.Range(pitch_min, pitch_max)`)
- 3D 설정 (`minDistance`, `maxDistance` 적용)
- AudioSource 풀 관리 (SoundChannel로 위임 유지)

---

## See Also

- `skills/devian-unity/30-unity-components/16-sound-tables/SKILL.md` — TB_SOUND 테이블 컬럼 규약
- `skills/devian-unity/30-unity-components/17-sound-manager/SKILL.md` — SoundManager 규약
- `skills/devian-unity/30-unity-components/18-voice-table-resolve/SKILL.md` — VoiceManager 규약
- `skills/devian-unity/30-unity-components/19-sound-domain/SKILL.md` — Sound 도메인 설계
