# 18-voice-table-resolve

Status: ACTIVE
AppliesTo: v10
Type: Component

## Purpose

Voice는 TB_VOICE 단일 테이블을 **로딩 시점에 "현재 언어용 맵"으로 Resolve 캐시**하고, 재생 시점에는 캐시 조회만 한다.

---

## Domain Ownership

- VoiceManager는 **Sound 도메인 소유**이다.
- TB_VOICE 테이블은 Sound 도메인에 귀속된다.
- SoundVoiceTableRegistry를 통해 VoiceManager와 TB_VOICE가 연결된다.
- 자세한 도메인 구조는 `19-sound-domain/SKILL.md` 참조.

---

## Hard Rules

### SystemLanguage 사용 제한 (가장 중요)

- **SystemLanguage는 로딩/초기화(Resolve) 단계에서만 사용한다.**
- **재생 시점에 SystemLanguage로 분기하는 메서드는 금지.**
  - 금지 예: `GetClipPath(SystemLanguage lang)`
- Voice 재생은 **voice_id → IVoiceRow 캐시 맵을 통해서만** 수행한다.

### 언어 변경 시 처리

- 언어 변경 시에는 **캐시를 재구성**한다.
- 필요하다면 관련 사운드 로딩 정책도 갱신한다.

### 테이블 분리 금지

- TB_VOICE는 단일 테이블을 유지한다.
- `VOICE_ko`, `VOICE_en` 같은 언어별 테이블 분리 금지.

### VOICE 독립 운용 (SOUND 미참조, Hard Rule)

- **VOICE는 SOUND 테이블을 참조하지 않는다.**
- **Resolve 결과는 sound_id가 아니라 clipPath(AudioClip 에셋 경로/키)**이다.
- clip 경로는 IVoiceRow.TryGetClipColumn()으로 직접 조회한다.
- sound_id 매핑 방식은 더 이상 사용하지 않는다.
- 언어 fallback 대상도 sound_id가 아니라 **clipPath**이다.
- VOICE 테이블의 key_bundle로 직접 번들 로드.

### VOICE 독립 로딩 규약 (Hard Rule)

- VoiceManager는 SOUND 테이블/row_id/sound_id에 의존하지 않는다.
- **로드/언로드는 `key_bundle` 단위로만 수행**한다 (key_group 제거됨).
- voice_id → clipPath 캐시/해결 흐름:
  1. ResolveForLanguage()에서 voice_id → IVoiceRow 캐시 구성
  2. IVoiceRow.TryGetClipColumn()으로 clipPath 조회
  3. clipPath로 AudioClip 로드

### Voice 로딩 책임 (Hard Rule)

- **VoiceManager가 Voice clip 로딩을 담당한다.**
- SoundManager는 Voice 채널을 로드하지 않는다.
- `LoadByBundleKeyAsync(bundleKey, language, fallbackLanguage)`로 key_bundle 단위 로드.
- Resolve 결과로 나온 IVoiceRow들만 로드한다.
- 언로드는 `UnloadByBundleKey(bundleKey)`로 수행.

### runtime_id 기반 재생 (Hard Rule)

- **PlayVoice()는 SoundRuntimeId를 반환한다.**
- 모든 재생 제어(Stop/Pause/Resume)는 runtime_id로 수행한다.
- SoundPlay/AudioSource를 외부에 노출하지 않는다.

### 3D Voice Play (Hard Rule)

- **PlayVoice3D(voiceId, position)** API를 제공한다.
- 3D 파라미터는 IVoiceRow의 distance_near/distance_far에서 가져온다.
- BaseAudioManager를 통해 재생한다.
- Linear rolloff, doppler=0 적용.
- **3D 적용 조건**: `effective3d = row.is3d && position.HasValue`

### VOICE 테이블 상수 (Hard Rule)

VOICE는 아래 값을 테이블 컬럼 없이 코드에서 상수로 반환한다:
- `isBundle = true` (항상 Bundle)
- `channel = SoundChannelType.Voice` (항상 Voice)
- `loop = false` (항상 비루프)
- `volume_scale = 1f`
- `pitch_min = 1f`, `pitch_max = 1f`

> VOICE는 isBundle/channel/loop 컬럼을 테이블에 두지 않고 코드에서 상수로 간주한다.

---

## Resolve Policy

### 로딩 단계 수행 내용

```csharp
// 1. 현재 언어에 해당하는 컬럼명 결정
string col = "clip_" + currentLanguage.ToString();  // 예: "clip_Korean"

// 2. 모든 TB_VOICE row 순회
foreach (var row in TB_VOICE.All())
{
    // 3. 해당 언어 컬럼에서 clip 경로 읽기
    if (!row.TryGetClipColumn(col, out var clipPath))
    {
        // 4. 비어있으면 fallback 적용
        if (!row.TryGetClipColumn("clip_English", out clipPath))
        {
            // 5. 둘 다 없으면 경고 후 스킵
            continue;
        }
    }

    // 6. 캐시 생성 (voice_id → IVoiceRow)
    _resolvedVoiceRows[row.voice_id] = row;
}
```

### 테이블 컨테이너 접근 방식

- **Reflection 금지** (성능/GC 문제)
- 컬럼명 → 값 해석은 `TryGetClipColumn()` switch문으로 수행
- 지원 언어 컬럼만 switch로 매칭 (Korean, English, Japanese, Chinese)
- **중국어 통합**: `SystemLanguage.Chinese` → `clip_Chinese` (간체/번체 구분 없음)

---

## Loading Policy

### LoadByBundleKeyAsync / UnloadByBundleKey (Hard Rule)

```csharp
// Voice 로드 (key_bundle 기반)
yield return VoiceManager.Instance.LoadByBundleKeyAsync(
    "voice_battle",              // TB_VOICE.key_bundle
    SystemLanguage.Korean,       // 언어
    SystemLanguage.English       // fallback 언어
);

// Voice 언로드
VoiceManager.Instance.UnloadByBundleKey("voice_battle");
```

- **로드/언로드는 `key_bundle` 단위로만 수행**한다 (key_group 제거됨).
- 반드시 **ResolveForLanguage() 호출 후**에 사용한다.
- key_bundle에 해당하는 voice rows에서 Resolve된 IVoiceRow만 로드.
- 내부적으로 `SoundManager._loadVoiceClipsAsync()` 호출.
- 언로드 시 `AssetManager.UnloadBundleAssets(bundleKey)` 호출.

### LoadByBundleKeysAsync / UnloadByBundleKeys (벌크)

```csharp
yield return VoiceManager.Instance.LoadByBundleKeysAsync(
    new[] { "voice_battle", "voice_ui" },
    SystemLanguage.Korean,
    SystemLanguage.English
);
VoiceManager.Instance.UnloadByBundleKeys(new[] { "voice_battle", "voice_ui" });
```

### 로드 순서 (중요)

1. `ResolveForLanguage(language)` - 전체 TB_VOICE Resolve
2. `LoadByBundleKeyAsync(bundleKey, language, fallback)` - 필요한 voice clip 로드
3. `PlayVoice(voiceId)` - 재생 (캐시 조회만)

---

## Playback API

### PlayVoice

```csharp
// 반환: runtime_id (재생 실패 시 Invalid)
SoundRuntimeId PlayVoice(
    string voiceId,
    float volume = 1f,
    float pitch = 0f,   // 0이면 row 기반 (VOICE는 항상 1f)
    int groupId = 0
)
```

- voice_id → IVoiceRow 캐시 조회
- SoundManager._playVoiceInternal(voiceRow, ...) 호출
- **반환값**: SoundRuntimeId (재생 실패 시 `SoundRuntimeId.Invalid`)

### 재생 제어 (runtime_id 기반)

```csharp
bool StopVoice(SoundRuntimeId runtimeId)
bool PauseVoice(SoundRuntimeId runtimeId)
bool ResumeVoice(SoundRuntimeId runtimeId)
bool IsVoicePlaying(SoundRuntimeId runtimeId)
```

- 모든 제어는 runtime_id로 수행
- 내부적으로 SoundManager의 해당 메서드 호출

### 자막 처리

```csharp
public string? GetCaptionKey(string voiceId)
{
    // voice_id 자체를 자막 키로 사용
    return voiceId;
}
```

- 자막 키는 voice_id 자체를 사용한다 (별도 컬럼 불필요).
- 자막 표시 시스템 자체는 이 스킬 범위 외.

### 3D Play API

```csharp
// 3D Voice 재생
SoundRuntimeId PlayVoice3D(
    string voiceId,
    Vector3 position,
    float volume = 1f,
    float pitch = 0f,
    int groupId = 0
)
```

- voice_id → IVoiceRow 캐시 조회
- SoundManager._playVoiceInternal(voiceRow, ..., position) 호출
- 3D 파라미터는 IVoiceRow.distance_near/distance_far 사용
- Linear rolloff, doppler=0

---

## Failure Policy

### voice_id 없음 또는 clip resolve 실패 시

**기본 정책 (개발 중 생산성 우선):**
- 경고 로그 출력
- 재생 스킵 (SoundRuntimeId.Invalid 반환)

**프로젝트 선택 옵션:**
- 빌드 실패 정책으로 전환 가능 (릴리즈 빌드용)

---

## Usage Examples

```csharp
// 1. 초기화 시점 (언어 Resolve)
VoiceManager.I.ResolveForLanguage(SystemLanguage.Korean);

// 2. Voice clip 로드 (key_bundle 기반)
yield return VoiceManager.I.LoadByBundleKeyAsync(
    "voice_battle",
    SystemLanguage.Korean,
    SystemLanguage.English
);

// 벌크 로드
yield return VoiceManager.I.LoadByBundleKeysAsync(
    new[] { "voice_battle", "voice_ui" },
    SystemLanguage.Korean,
    SystemLanguage.English
);

// 3. 재생 시점 (runtime_id 반환)
var runtimeId = VoiceManager.I.PlayVoice("VO_TUTORIAL_001");

// 4. runtime_id로 제어
VoiceManager.I.PauseVoice(runtimeId);
VoiceManager.I.ResumeVoice(runtimeId);
VoiceManager.I.StopVoice(runtimeId);

// 5. 재생 상태 확인
if (VoiceManager.I.IsVoicePlaying(runtimeId))
{
    // 재생 중...
}

// 6. 자막 키 조회 (voice_id 자체가 키)
string captionKey = VoiceManager.I.GetCaptionKey("VO_TUTORIAL_001");
string subtitle = StringTable.Get(captionKey);

// 7. 3D Voice 재생 (Linear rolloff)
var runtimeId3D = VoiceManager.I.PlayVoice3D("VO_TUTORIAL_001", transform.position);

// 8. 언로드 (key_bundle 기반)
VoiceManager.I.UnloadByBundleKey("voice_battle");
VoiceManager.I.UnloadByBundleKeys(new[] { "voice_battle", "voice_ui" });

// 9. 언어 변경 시 (Resolve 재수행 필요)
VoiceManager.I.ResolveForLanguage(SystemLanguage.English);
// + 필요한 bundle들 다시 로드
```

---

## Non-goals

- TB_SOUND/TB_VOICE 테이블 컬럼 규약은 `16-sound-tables` 스킬에서 다룬다.
- SoundManager 자체의 채널/풀/쿨타임 규약은 `17-sound-manager` 스킬에서 다룬다.

---

## See Also

- `skills/devian-unity/30-unity-components/19-sound-domain/SKILL.md` — **Sound 도메인 설계 (SSOT)**
- `skills/devian-unity/30-unity-components/16-sound-tables/SKILL.md` — TB_SOUND/TB_VOICE 테이블 규약
- `skills/devian-unity/30-unity-components/17-sound-manager/SKILL.md` — SoundManager 규약
- `skills/devian-unity/30-unity-components/20-base-audio-manager/SKILL.md` — BaseAudioManager 공통 Play 규약
- `skills/devian-unity/30-unity-components/14-table-manager/SKILL.md` — TableManager (테이블 로딩)
