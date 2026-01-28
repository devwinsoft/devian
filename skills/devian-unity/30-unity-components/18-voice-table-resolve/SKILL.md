# 18-voice-table-resolve

Status: ACTIVE
AppliesTo: v10
Type: Component

## Purpose

Voice는 TB_VOICE 단일 테이블을 **로딩 시점에 "현재 언어용 맵"으로 Resolve 캐시**하고, 재생 시점에는 캐시 조회만 한다.

---

## Hard Rules

### SystemLanguage 사용 제한 (가장 중요)

- **SystemLanguage는 로딩/초기화(Resolve) 단계에서만 사용한다.**
- **재생 시점에 SystemLanguage로 분기하는 메서드는 금지.**
  - 금지 예: `GetSoundId(SystemLanguage lang)`
- Voice 재생은 **voice_id → sound_id 캐시 맵을 통해서만** 수행한다.

### 언어 변경 시 처리

- 언어 변경 시에는 **캐시를 재구성**한다.
- 필요하다면 관련 사운드 로딩 정책도 갱신한다.

### 테이블 분리 금지

- TB_VOICE는 단일 테이블을 유지한다.
- `VOICE_ko`, `VOICE_en` 같은 언어별 테이블 분리 금지.

---

## Resolve Policy

### 로딩 단계 수행 내용

```csharp
// 1. 현재 언어에 해당하는 컬럼명 결정
string col = "clip_" + currentLanguage.ToString();  // 예: "clip_Korean"

// 2. 모든 TB_VOICE row 순회
foreach (var row in TB_VOICE.All())
{
    // 3. 해당 언어 컬럼에서 sound_id 읽기
    string soundId = row.GetColumn(col);

    // 4. 비어있으면 fallback 적용
    if (string.IsNullOrEmpty(soundId))
        soundId = row.GetColumn("clip_English");  // 또는 프로젝트 default

    // 5. 캐시 생성
    _voiceSoundIdByVoiceId[row.voice_id] = soundId;
    _subtitleKeyByVoiceId[row.voice_id] = row.text_l10n_key;
}
```

### 테이블 컨테이너 접근 방식

- **Reflection 금지** (성능/GC 문제)
- 컬럼명 → 인덱스 해석은 **Resolve 시점에 1회만** 수행 (가능하면)

---

## Playback Policy

### PlayVoice API

```csharp
public void PlayVoice(string voiceId, ...)
{
    // 1. 캐시에서 sound_id 조회
    if (!_voiceSoundIdByVoiceId.TryGetValue(voiceId, out var soundId))
    {
        Log.Warn($"Voice not found: {voiceId}");
        return;
    }

    // 2. SoundManager로 재생 위임
    SoundManager.I.Play(soundId, channelOverride: "Voice", ...);
}
```

### 자막 처리

```csharp
public string GetSubtitleKey(string voiceId)
{
    // text_l10n_key로 StringTable에서 자막 조회
    if (_subtitleKeyByVoiceId.TryGetValue(voiceId, out var key))
        return key;
    return null;
}
```

- 자막 표시 시스템 자체는 이 스킬 범위 외.

---

## Failure Policy

### voice_id 없음 또는 sound_id resolve 실패 시

**기본 정책 (개발 중 생산성 우선):**
- 경고 로그 출력
- 재생 스킵

**프로젝트 선택 옵션:**
- 빌드 실패 정책으로 전환 가능 (릴리즈 빌드용)

```csharp
// 프로젝트 설정 예시
public static class VoiceConfig
{
    public static bool FailOnMissingVoice = false;  // true면 예외 발생
}
```

---

## Usage Examples

```csharp
// 초기화 시점 (언어 설정 후)
VoiceManager.I.ResolveForLanguage(SystemLanguage.Korean);

// 재생 시점 (캐시 조회만)
VoiceManager.I.PlayVoice("VO_TUTORIAL_001");

// 자막 키 조회
string subtitleKey = VoiceManager.I.GetSubtitleKey("VO_TUTORIAL_001");
string subtitle = StringTable.Get(subtitleKey);

// 언어 변경 시
VoiceManager.I.ResolveForLanguage(SystemLanguage.English);
```

---

## Non-goals

- TB_SOUND/TB_VOICE 테이블 컬럼 규약은 `16-sound-tables` 스킬에서 다룬다.
- SoundManager 자체의 채널/풀/쿨타임 규약은 `17-sound-manager` 스킬에서 다룬다.

---

## See Also

- `skills/devian-unity/30-unity-components/16-sound-tables/SKILL.md` — TB_SOUND/TB_VOICE 테이블 규약
- `skills/devian-unity/30-unity-components/17-sound-manager/SKILL.md` — SoundManager 규약
- `skills/devian-unity/30-unity-components/14-table-manager/SKILL.md` — TableManager (테이블 로딩)
