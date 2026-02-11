# 20-base-audio-manager

Status: ACTIVE
AppliesTo: v10
Type: Component

## Purpose

BaseAudioManager는 2D/3D 사운드 재생의 공통 로직을 제공한다.
SoundManager/VoiceManager는 Play 로직을 직접 구현하지 않고, BaseAudioManager에 위임한다.

**책임 분리 규약:**
- 테이블은 SOUND/VOICE 분리 (각각 독립 테이블)
- 공통 로직(볼륨/피치 계산, 3D 설정, 채널 플레이)은 BaseAudioManager가 담당

---

## Hard Rules

### 공통 베이스 원칙

- **공통 베이스 이름은 `BaseAudioManager`**이다.
- SoundManager/VoiceManager는 Play 로직(볼륨/피치 계산, 3D 설정)을 직접 구현하지 않는다.
- 볼륨/피치 계산, 3D 파라미터 구성, SoundChannel 재생 위임은 전부 BaseAudioManager가 담당한다.

### IAudioRowBase 기반

- BaseAudioManager는 **IAudioRowBase 인터페이스**를 받아 처리한다.
- ISoundRow와 IVoiceRow 모두 IAudioRowBase를 구현한다.
- VOICE는 isBundle/channel/loop/volume_scale/pitch_* 를 상수로 반환한다.

### 3D 파라미터 SSOT

**3D 파라미터의 SSOT는 IAudioRowBase 필드**이다. 아래 필드를 사용한다:

| 필드명 | 타입 | 설명 | 기본값 |
|--------|------|------|--------|
| `is3d` | bool | 3D 여부 | false |
| `distance_near` | float | 3D near 거리 (= minDistance) | 1.0 |
| `distance_far` | float | 3D far 거리 (= maxDistance) | 500.0 |
| `volume_scale` | float | 최종 볼륨 스케일 | 1.0 |
| `pitch_min` | float | 피치 랜덤 범위 최소 | 1.0 |
| `pitch_max` | float | 피치 랜덤 범위 최대 | 1.0 |
| `loop` | bool | 루프 여부 | false |
| `cooltime` | float | 쿨타임 (초) | 0.0 |

### 3D 설정: Linear Rolloff (Hard Rule)

3D일 때 아래를 강제 적용한다:
- `AudioSource.rolloffMode = AudioRolloffMode.Linear`
- `AudioSource.dopplerLevel = 0` (도플러 효과 비활성화)
- `AudioSource.spatialBlend = 1f`
- `minDistance = row.distance_near`
- `maxDistance = row.distance_far`

2D일 때:
- `AudioSource.spatialBlend = 0f`

**3D 적용 조건 (Hard Rule):**
- `effective3d = row.is3d && position.HasValue`
- position이 있어도 row.is3d가 false면 2D로 재생됨

### Play 파라미터 적용 규칙

1. **볼륨**: `finalVolume = externalVolume * row.volume_scale`
2. **피치**:
   - pitchOverride != 0이면 사용
   - 아니면 `pitch_min < pitch_max`이면 `Random.Range(pitch_min, pitch_max)`
   - 아니면 `pitch_min` (또는 1.0)
3. **3D 판정**: `effective3d = row.is3d && position.HasValue`
4. **3D 거리**: `minDistance = row.distance_near`, `maxDistance = row.distance_far`

### VOICE 기본값/weight 분리 규약 (Hard Rule)

- **VOICE는 기본값 상수로 처리된다:**
  - `isBundle = true`
  - `channel = SoundChannelType.Voice`
  - `loop = false`
- **weight는 SOUND 전용**이며 BaseAudioManager 공통 규약에는 포함하지 않는다.
- BaseAudioManager는 weight 기반 랜덤 선택을 수행하지 않는다 (SoundManager 책임).

---

## API

### ComputePlayParams (static)

```csharp
/// <summary>
/// 볼륨/피치/3D 파라미터를 계산한다.
/// </summary>
public static void ComputePlayParams(
    IAudioRowBase row,
    float externalVolume,
    float pitchOverride,
    Vector3? position,
    out float finalVolume,
    out float finalPitch,
    out bool effective3d
)
```

### TryPlay (static)

```csharp
/// <summary>
/// SoundChannel에 재생 요청을 위임한다.
/// </summary>
public static bool TryPlay(
    SoundChannel channel,
    SoundRuntimeId runtimeId,
    string logicalId,        // Sound: soundId, Voice: voiceId
    int rowId,               // Sound: row_id, Voice: voice_id 해시
    IAudioRowBase row,
    AudioClip clip,
    float externalVolume,
    float pitchOverride,     // 0이면 row 기반
    int groupId,
    Vector3? position        // null이면 2D
)
```

- ComputePlayParams()로 볼륨/피치/3D 계산
- SoundChannel.PlayWithRuntimeId()에 모든 파라미터 전달

---

## Architecture

```
[SoundManager]  [VoiceManager]
      |               |
      v               v
+----------------------------------+
|        BaseAudioManager          |
| - ComputePlayParams(row, ...)    |
| - TryPlay(channel, row, ...)     |
+----------------------------------+
           |
           v
    [SoundChannel]
           |
           v
     [SoundPlay]
        - Linear rolloff
        - doppler = 0
```

---

## Implementation Notes

### BaseAudioManager 구현 방식

BaseAudioManager는 **internal static class**로 구현한다:
- SoundManager/VoiceManager 내부에서만 호출
- 외부 API로 노출하지 않음
- 상속/오버라이드 불필요

### SoundPlay 3D 설정

SoundPlay.Play() 호출 시 3D 설정:
```csharp
if (is3d && position.HasValue)
{
    _audioSource.spatialBlend = 1f;
    _audioSource.rolloffMode = AudioRolloffMode.Linear;
    _audioSource.dopplerLevel = 0f;
    _audioSource.minDistance = distanceNear;
    _audioSource.maxDistance = distanceFar;
    transform.position = position.Value;
}
```

### AudioAssetNameUtil

에셋 경로 처리를 위한 유틸리티:
- `ExtractAssetName(path)`: 경로에서 파일명 추출 (확장자 제외)
- `RemoveExtension(path)`: Resources 경로에서 확장자만 제거

---

## See Also

- `skills/devian-unity/22-sound-system/16-sound-tables/SKILL.md` — TB_SOUND 테이블 컬럼 규약
- `skills/devian-unity/22-sound-system/17-sound-manager/SKILL.md` — SoundManager 규약
- `skills/devian-unity/22-sound-system/18-voice-table-resolve/SKILL.md` — VoiceManager 규약
- `skills/devian-unity/22-sound-system/19-sound-domain/SKILL.md` — Sound 도메인 설계
