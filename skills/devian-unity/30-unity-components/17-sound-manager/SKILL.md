# 17-sound-manager

Status: ACTIVE
AppliesTo: v10
Type: Component

## Purpose

SoundManager는 테이블 기반으로 사운드 재생/풀/채널/쿨타임을 관리한다.

---

## Domain Ownership

- SoundManager/VoiceManager/SoundChannel/SoundPlay는 **Sound 도메인 소유**이다.
- TB_SOUND/TB_VOICE 테이블은 Sound 도메인 DLL에 위치한다.
- SoundVoiceTableRegistry는 Sound 도메인에서 테이블과 매니저를 연결한다.
- 자세한 도메인 구조는 `19-sound-domain/SKILL.md` 참조.

---

## Architecture

| 컴포넌트 | 책임 |
|----------|------|
| **SoundManager** (전역) | 채널 생성/관리, 테이블 인덱싱, 로딩 그룹 정책, runtime_id 발급/추적 |
| **SoundChannel** (채널) | 풀/재생 리스트/쿨타임/SEQ 관리, runtime_id 기반 제어 |
| **SoundPlay** (재생 유닛) | Wait/FadeIn/Play/FadeOut 상태 처리, generation 기반 풀 재사용 보호 |

---

## Hard Rules

### 테이블 기반 식별

- SoundManager는 **테이블(TB_SOUND) 기반으로만 식별**한다.
- 직접 경로 문자열 하드코딩 금지.

### Voice 채널 책임 분리 (Hard Rule)

- **SoundManager는 Voice 채널을 로드하지 않는다.**
- `LoadByKeyAsync()`는 `channel == "Voice"` row를 제외하고 로드한다.
- Voice 로딩은 **VoiceManager.LoadByGroupKeyAsync()** 가 담당한다.
- SoundManager 공개 API에는 **언어(SystemLanguage) 파라미터가 없다.**
- Voice 언어 결정은 VoiceManager의 책임이다.

### runtime_id 기반 재생 인스턴스 관리 (Hard Rule)

- `PlaySound()`/`PlayVoice()`는 **SoundRuntimeId** (readonly struct)를 반환한다.
- 모든 재생 제어(Stop/Pause/Resume/SetVolume/SetPitch)는 **runtime_id로 수행**한다.
- SoundPlay/AudioSource를 외부에 노출하지 않는다.
- generation 기반으로 풀 재사용 시 잘못된 제어를 방지한다.

### PlaySound API

- `SoundManager.PlaySound(...)`는 입력으로 `sound_id`를 받는다.
- 내부에서 **동일 sound_id를 가진 후보 rows를 조회**하고, **weight 기반 랜덤으로 1개를 선택**하여 클립을 재생한다.
- 선택된 row의 `row_id`로 clipCache에서 AudioClip을 resolve한다.
- 쿨타임은 논리키(`sound_id`) 단위로 적용한다 (같은 sound_id의 모든 변형 row가 쿨타임 공유).
- **반환값**: SoundRuntimeId (재생 실패 시 `SoundRuntimeId.Invalid`)

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

## SoundRuntimeId

### 구조

```csharp
public readonly struct SoundRuntimeId
{
    public readonly int Value;
    public bool IsValid => Value > 0;
    public static SoundRuntimeId Invalid => new(0);
}
```

- 값 타입 (readonly struct)
- 0 이하는 무효 (Invalid)
- PlaySound/PlayVoice 반환값

### generation 기반 풀 재사용 보호

```csharp
// SoundPlay 내부
private int _generation;

public void Acquire(SoundRuntimeId runtimeId, ...)
{
    _generation++;  // 풀에서 꺼낼 때마다 증가
    _runtimeId = runtimeId;
}

public bool ValidateGeneration(int expected) => _generation == expected;
```

- SoundChannel이 `(SoundPlay, generation)` 쌍으로 추적
- 제어 요청 시 generation 일치 여부 검증
- 불일치하면 이미 풀에 반환된 것이므로 명령 무시

---

## Playback API

### PlaySound

```csharp
// 반환: runtime_id (재생 실패 시 Invalid)
SoundRuntimeId PlaySound(
    string soundId,
    float volume = 1f,
    float pitch = 1f,
    int groupId = 0,
    Vector3? position = null,
    string? channelOverride = null
)
```

### 재생 제어 (runtime_id 기반)

```csharp
bool StopSound(SoundRuntimeId runtimeId)
bool PauseSound(SoundRuntimeId runtimeId)
bool ResumeSound(SoundRuntimeId runtimeId)
bool SetSoundVolume(SoundRuntimeId runtimeId, float volume)
bool SetSoundPitch(SoundRuntimeId runtimeId, float pitch)
bool IsPlaying(SoundRuntimeId runtimeId)
bool TryGetPlayingInfo(SoundRuntimeId runtimeId, out PlayingInfo info)
```

- 모든 제어는 runtime_id로 수행
- 반환값 bool: 성공 여부 (이미 종료된 사운드는 false)

### 벌크 제어

```csharp
int StopAllBySoundId(string soundId)       // sound_id 기준 전체 정지
int StopAllByChannel(SoundChannelType type) // 채널 기준 전체 정지
void StopAll()                              // 전체 정지
```

---

## Loading Policy

### LoadByKeyAsync / UnloadByKey

```csharp
yield return SoundManager.Instance.LoadByKeyAsync("GLOBAL");
SoundManager.Instance.UnloadByKey("BATTLE");
```

- `TB_SOUND.key`로 **row 전체**(row_id 단위)를 등록/해제한다.
- **Voice 채널(channel == "Voice")은 제외**한다.
- 로딩: `GetSoundRowsByKey(key)` → Voice 제외 row 순회 → `bundle_key` 기준 로드 → `row_id`로 clipCache 등록
- 언로딩: `key`에 해당하는 row_id 목록의 clipCache 제거
- `source` / `bundle_key` 규칙으로 에셋 로딩/언로딩을 수행한다.

### _loadVoiceBySoundIdsAsync (internal)

- VoiceManager 전용 Voice clip 로드 헬퍼.
- Resolve된 `sound_id` 집합을 받아 해당 Voice row만 로드.
- 공개 API로 노출하지 않는다 (internal).

```csharp
// VoiceManager가 호출
yield return SoundManager.Instance._loadVoiceBySoundIdsAsync(
    "VOICE::BATTLE",      // voiceGroupKey
    resolvedSoundIds,     // sound_id 집합
    language,
    fallbackLanguage
);
```

### source == Bundle

- `bundle_key` 기준으로 로딩 단위를 잡는다.
- 구현 상세는 코드에 위임하되 규약만 고정.

---

## SoundVoiceTableRegistry

### 위치 및 책임

- **SoundVoiceTableRegistry는 Sound 도메인에 존재**한다.
- `RuntimeInitializeOnLoadMethod(BeforeSceneLoad)`로 매니저와 테이블을 연결한다.
- 현재는 Game 도메인에 위치하지만, **Phase 2에서 Sound 도메인으로 이동** 예정.

### Adapter 패턴 (Hard Rule)

- **SoundVoiceTableRegistry**는 Generated 테이블(TB_SOUND, TB_VOICE)과 Manager(SoundManager, VoiceManager)를 연결한다.
- Generated 클래스(SOUND, VOICE)는 PascalCase 프로퍼티를 사용하고, Manager는 ISoundRow/IVoiceRow 인터페이스(snake_case)를 기대한다.
- **Adapter 패턴**으로 변환한다:
  - `SoundRowAdapter`: SOUND → ISoundRow
  - `VoiceRowAdapter`: VOICE → IVoiceRow
- Adapter 인스턴스는 **캐시**하여 동일 row에 대해 중복 생성하지 않는다.

### 테이블 로드 후 인덱스 빌드

- TB 로더에서 `LoadFromNdjson` / `LoadFromPb64Binary` 호출 후 **반드시 `BuildGroupIndices()` 호출**.
- 로드 시 Adapter 캐시도 클리어한다.

```csharp
// SoundVoiceTableRegistry 내부
TableManager.Instance.RegisterTbLoader("SOUND", (format, text, bin) =>
{
    _soundAdapterCache.Clear();
    // ... 로드 로직 ...
    TB_SOUND.BuildGroupIndices();
});
```

---

## Usage Examples

```csharp
// 글로벌 사운드 로드
yield return SoundManager.I.LoadByKeyAsync("GLOBAL");

// 기본 재생 (runtime_id 반환)
var runtimeId = SoundManager.I.PlaySound("UI_CLICK");

// 볼륨/그룹 지정 재생
var hitId = SoundManager.I.PlaySound("SFX_HIT", volume: 0.8f, groupId: 123);

// 3D 사운드 재생
var explosionId = SoundManager.I.PlaySound("SFX_EXPLOSION", position: transform.position);

// runtime_id로 제어
SoundManager.I.PauseSound(runtimeId);
SoundManager.I.ResumeSound(runtimeId);
SoundManager.I.SetSoundVolume(runtimeId, 0.5f);
SoundManager.I.StopSound(runtimeId);

// 재생 상태 확인
if (SoundManager.I.IsPlaying(hitId))
{
    // 재생 중...
}

// 재생 정보 조회
if (SoundManager.I.TryGetPlayingInfo(hitId, out var info))
{
    Log.Info($"Playing {info.SoundId} at volume {info.Volume}");
}

// 벌크 제어
SoundManager.I.StopAllBySoundId("BGM_BATTLE");  // 특정 sound_id 전체 정지
SoundManager.I.StopAllByChannel(SoundChannelType.Effect);  // 채널 전체 정지

// 언로드
SoundManager.I.UnloadByKey("BATTLE");
```

---

## Non-goals

- Voice 로컬라이징 규칙은 여기서 다루지 않는다.
- Voice 관련 규칙은 `18-voice-table-resolve` 스킬로 위임 (중복 방지).

---

## See Also

- `skills/devian-unity/30-unity-components/19-sound-domain/SKILL.md` — **Sound 도메인 설계 (SSOT)**
- `skills/devian-unity/30-unity-components/16-sound-tables/SKILL.md` — TB_SOUND/TB_VOICE 테이블 규약
- `skills/devian-unity/30-unity-components/18-voice-table-resolve/SKILL.md` — Voice Resolve 규약
