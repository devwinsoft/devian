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
| **SoundPlay** (재생 유닛) | Wait/FadeIn/Play/FadeOut 상태 처리, generation 기반 풀 재사용 보호, 3D Linear rolloff 설정 |
| **BaseAudioManager** (static) | 볼륨/피치 계산, 3D 파라미터 구성, SoundChannel에 재생 요청 위임 |
| **AudioAssetNameUtil** (static) | 에셋 경로에서 이름 추출, 확장자 제거 유틸리티 |

---

## Hard Rules

### 테이블 기반 식별

- SoundManager는 **테이블(TB_SOUND) 기반으로만 식별**한다.
- 직접 경로 문자열 하드코딩 금지.

### SoundManager는 SOUND 테이블만 담당 (Hard Rule)

- **SoundManager는 TB_SOUND 테이블만 다룬다.**
- VOICE 재생/로딩과 결합 금지 (VoiceManager 독립).
- VOICE 관련 로직은 VoiceManager가 담당한다.

### Voice 채널 책임 분리 (Hard Rule)

- **SoundManager는 Voice 채널을 로드하지 않는다.**
- `LoadByBundleKeyAsync()`는 `channel == SoundChannelType.Voice` row를 제외하고 로드한다.
- 채널 비교는 **enum 기반**이다 (문자열 비교 금지): `SoundChannelType.Voice`
- Voice 로딩은 **VoiceManager.LoadByBundleKeyAsync()** 가 담당한다.
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

### key_bundle 기반 로드/언로드 (Hard Rule)

| 필드 | 용도 | 예시 |
|------|------|------|
| `key_bundle` | **로드/언로드 단위 키** | `"sound_battle"`, `"sound_ui"` |

- **key_group은 제거됨** — 로드/언로드는 `key_bundle` 단위로만 수행한다.
- `isBundle=true`: Addressables label/key로 사용
- `isBundle=false`: Resources 그룹 키로 사용 (AssetManager Resource API 기반)

### isBundle 기반 로딩 분기 (Hard Rule)

- `row.isBundle == true`: Addressables/AssetBundle로 로드 (key_bundle 단위)
- `row.isBundle == false`: AssetManager Resource API로 로드 (key_bundle를 Resource 그룹 키로 사용)
- 확장자 제거는 `AudioAssetNameUtil.RemoveExtension()` 사용
- SOUND 테이블의 `source` (string enum) 방식은 **폐기됨** → `isBundle` (bool)로 교체
- **Resources.Load 직접 호출 금지** — 언로드를 위해 AssetManager 캐시에 등록되어야 함

### 채널/풀 책임 분리

- 채널/풀/쿨타임은 **SoundChannel 책임**이다.
- SoundManager가 AudioSource를 직접 들고 운용하지 않는다.

### SerializeField 의존 최소화

- SoundManager는 **SerializeField 의존 없이 런타임 생성 원칙**을 권장한다.
- 프로젝트 정책에 따라 조정 가능하나, 스킬은 "참조 의존 최소화"로 고정.

### BaseAudioManager 위임 (Hard Rule)

- **SoundManager/VoiceManager는 Play 로직을 직접 구현하지 않는다.**
- 볼륨/피치 계산, 3D 설정 파라미터 구성은 **BaseAudioManager가 담당**한다.
- BaseAudioManager.TryPlay()가 SoundChannel에 재생 요청을 위임한다.
- 3D 파라미터 (`distance_near`, `distance_far`)는 IAudioRowBase 필드에서 가져온다.
- 자세한 내용은 `20-base-audio-manager/SKILL.md` 참조.

### 3D 설정: Linear Rolloff (Hard Rule)

- 3D 사운드는 **Linear rolloff** 방식을 사용한다.
- `AudioSource.rolloffMode = AudioRolloffMode.Linear`
- `AudioSource.dopplerLevel = 0` (도플러 효과 비활성화)
- `distance_near` = minDistance, `distance_far` = maxDistance

### 3D 적용 판단 규칙 (Hard Rule)

- 3D는 position만으로 결정하지 않고 **row.is3d를 포함해 결정**한다:
- `effective3d = row.is3d && position.HasValue`
- position이 있어도 row.is3d가 false면 2D로 재생된다.

### weight는 SOUND 전용 (Hard Rule)

- `weight`는 동일 sound_id 후보 rows 중 **가중치 기반 랜덤 선택** 용도이다.
- VOICE 테이블에는 weight 필드가 없으며 사용하지 않는다.

### channel은 enum 기반 (Hard Rule)

- `channel`은 string이 아니라 **SoundChannelType enum**으로 취급한다.
- 테이블 값도 enum 이름과 일치해야 한다: `Bgm`, `Effect`, `Ui`, `Voice`
- 문자열 비교 방식은 사용하지 않는다.

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

### PlaySound (2D)

```csharp
// 반환: runtime_id (재생 실패 시 Invalid)
SoundRuntimeId PlaySound(
    string soundId,
    float volume = 1f,
    float pitch = 0f,   // 0이면 row 기반 랜덤
    int groupId = 0,
    SoundChannelType? channelOverride = null
)
```

### PlaySound3D (3D)

```csharp
// 3D 사운드 재생 - 위치 필수
SoundRuntimeId PlaySound3D(
    string soundId,
    Vector3 position,
    float volume = 1f,
    float pitch = 0f,
    int groupId = 0,
    SoundChannelType? channelOverride = null
)
```

- 3D 파라미터(`distance_near`, `distance_far`)는 SOUND row에서 자동 적용됨
- BaseAudioManager.TryPlay()로 위임
- Linear rolloff, doppler=0

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

### LoadByBundleKeyAsync / UnloadByBundleKey (Hard Rule)

```csharp
yield return SoundManager.Instance.LoadByBundleKeyAsync("sound_battle");
SoundManager.Instance.UnloadByBundleKey("sound_battle");
```

- **로드/언로드는 `key_bundle` 단위로만 수행**한다 (key_group 제거됨).
- **Voice 채널(channel == SoundChannelType.Voice)은 제외**한다.
- isBundle=true: `AssetManager.LoadBundleAssets<AudioClip>(key_bundle)`
- isBundle=false: `AssetManager.LoadResourceAssets<AudioClip>(key_bundle)`
- 로딩: `GetSoundRowsByBundleKey(bundleKey)` → Voice 제외 row 순회 → 로드 → `row_id`로 clipCache 등록
- 언로딩: `_loadedRowIdsByBundleKey[bundleKey]` 목록의 clipCache 제거 + AssetManager 언로드

### LoadByBundleKeysAsync / UnloadByBundleKeys (벌크)

```csharp
yield return SoundManager.Instance.LoadByBundleKeysAsync(new[] { "sound_battle", "sound_ui" });
SoundManager.Instance.UnloadByBundleKeys(new[] { "sound_battle", "sound_ui" });
```

### Unload 동기화 규칙 (Hard Rule)

- `UnloadByBundleKey`는 **row 캐시 제거 + AssetManager 언로드를 동기화**한다.
- isBundle=true: `AssetManager.UnloadBundleAssets(key_bundle)` 호출
- isBundle=false: `AssetManager.UnloadResourceAssets<AudioClip>(key_bundle)` 호출
- 언로드 후 `_loadedRowIdsByBundleKey[bundleKey]` 및 `_loadedBundleKeys` 에서 제거

### 캐시 구조

```csharp
// key_bundle 기반 캐시
private readonly Dictionary<string, List<int>> _loadedRowIdsByBundleKey;
private readonly HashSet<string> _loadedBundleKeys;
private readonly Dictionary<int, AudioClip> _clipCacheByRowId;
```

### _loadVoiceClipsAsync (internal)

- VoiceManager 전용 Voice clip 로드 헬퍼.
- IVoiceRow 집합을 받아 해당 Voice clip을 로드.
- **VOICE는 SOUND 테이블을 참조하지 않고 독립적으로 로드**한다.
- 공개 API로 노출하지 않는다 (internal).

```csharp
// VoiceManager가 호출
yield return SoundManager.Instance._loadVoiceClipsAsync(
    bundleKey,            // key_bundle
    resolvedVoiceRows,    // IVoiceRow 집합
    language,
    fallbackLanguage
);
```

---

## SoundVoiceTableRegistry

### 위치 및 책임

- **SoundVoiceTableRegistry는 Sound 도메인에 존재**한다.
- `RuntimeInitializeOnLoadMethod(BeforeSceneLoad)`로 매니저와 테이블을 연결한다.

### Adapter 패턴 (Hard Rule)

- **SoundVoiceTableRegistry**는 Generated 테이블(TB_SOUND, TB_VOICE)과 Manager(SoundManager, VoiceManager)를 연결한다.
- Generated 클래스(SOUND, VOICE)는 PascalCase 프로퍼티를 사용하고, Manager는 ISoundRow/IVoiceRow 인터페이스(snake_case)를 기대한다.
- **Adapter 패턴**으로 변환한다:
  - `SoundRowAdapter`: SOUND → ISoundRow
  - `VoiceRowAdapter`: VOICE → IVoiceRow
- Adapter 인스턴스는 **캐시**하여 동일 row에 대해 중복 생성하지 않는다.

### Delegate 연결

```csharp
// SoundManager
SoundManager.Instance.GetSoundRowsBySoundId = GetSoundRowsBySoundId;
SoundManager.Instance.GetSoundRowsByBundleKey = GetSoundRowsByBundleKey;

// VoiceManager
VoiceManager.Instance.GetVoiceRow = GetVoiceRow;
VoiceManager.Instance.GetAllVoiceRows = GetAllVoiceRows;
VoiceManager.Instance.GetVoiceRowsByBundleKey = GetVoiceRowsByBundleKey;
```

### 테이블 로드 후 인덱스 빌드

- TB 로더에서 `LoadFromNdjson` / `LoadFromPb64Binary` 호출 후 **반드시 `BuildBundleIndices()` 호출**.
- 로드 시 Adapter 캐시도 클리어한다.

---

## Usage Examples

```csharp
// 번들 키로 사운드 로드
yield return SoundManager.I.LoadByBundleKeyAsync("sound_global");
yield return SoundManager.I.LoadByBundleKeysAsync(new[] { "sound_battle", "sound_ui" });

// 기본 재생 (runtime_id 반환)
var runtimeId = SoundManager.I.PlaySound("UI_CLICK");

// 볼륨/그룹 지정 재생
var hitId = SoundManager.I.PlaySound("SFX_HIT", volume: 0.8f, groupId: 123);

// 3D 사운드 재생 (Linear rolloff)
var explosionId = SoundManager.I.PlaySound3D("SFX_EXPLOSION", transform.position);

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

// 번들 키로 언로드
SoundManager.I.UnloadByBundleKey("sound_battle");
SoundManager.I.UnloadByBundleKeys(new[] { "sound_battle", "sound_ui" });
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
- `skills/devian-unity/30-unity-components/20-base-audio-manager/SKILL.md` — BaseAudioManager 공통 Play 규약
