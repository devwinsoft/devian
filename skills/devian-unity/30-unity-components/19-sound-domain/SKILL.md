# 19-sound-domain

Status: ACTIVE
AppliesTo: v10
Type: Architecture

## Purpose

Sound 도메인은 사운드/음성 관련 기능(테이블, 매니저, 레지스트리)을 단일 책임 영역으로 묶어 관리한다.
이 스킬은 Sound 도메인의 **최종 구조(SSOT)**를 정의한다.

---

## Hard Rules

### Sound 도메인이 SSOT (Single Source of Truth)

- Sound 기능의 SSOT는 **Sound 도메인**이다.
- Sound/Voice 테이블, 레지스트리, 매니저를 **같은 도메인/같은 DLL(asmdef)**로 묶는다.
- 다른 도메인(Game 등)은 Sound 도메인에 의존할 수 있으나, **Sound → Game 참조는 금지**.

### 의존성 방향 (Dependency Rule)

```
[Game Domain] ──depends──► [Sound Domain] ──depends──► [Foundation]
```

- Game 도메인 → Sound 도메인: **허용**
- Sound 도메인 → Game 도메인: **금지**
- Sound 도메인 → Foundation: **허용**

---

## 최종 구조 (Target Structure)

### 테이블 소유

| 테이블 | 테이블 키 | 소유 도메인 | 네임스페이스 (목표) |
|--------|----------|------------|-------------------|
| TB_SOUND | `SOUND` | Sound 도메인 | `Devian.Domain.Sound` |
| TB_VOICE | `VOICE` | Sound 도메인 | `Devian.Domain.Sound` |

- 테이블 키(`SOUND`, `VOICE`)는 기존과 동일하게 유지
- Generated 클래스의 네임스페이스 목표: `Devian.Domain.Sound`

### 매니저 배치

| 클래스 | 소유 도메인 | 네임스페이스 |
|--------|------------|-------------|
| `SoundManager` | Sound 도메인 | `Devian` (루트 유지) |
| `VoiceManager` | Sound 도메인 | `Devian` (루트 유지) |

- 매니저는 Sound 도메인 DLL에 포함
- 네임스페이스는 기존 정책 유지 (`Devian` 루트)
- 호환성을 위해 namespace 변경은 선택적

### 레지스트리 배치

| 클래스 | 소유 도메인 | 네임스페이스 (목표) |
|--------|------------|-------------------|
| `SoundVoiceTableRegistry` | Sound 도메인 | `Devian.Domain.Sound` |

### 데이터 파일 위치

| 파일 | 현재 위치 | 목표 위치 |
|------|----------|----------|
| `SoundTable.xlsx` | `input/Domains/Game/tables/` | `input/Domains/Sound/tables/` |

> **Note**: Phase 1에서는 파일 이동 금지. Phase 2에서 이동.

---

## 레지스트리 정책 (Critical)

### SoundVoiceTableRegistry 책임

`SoundVoiceTableRegistry`는 Sound 도메인에 존재하며, 다음을 수행한다:

```csharp
[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
private static void _register()
{
    // 1. SoundManager 델리게이트 연결
    SoundManager.Instance.GetSoundRowsBySoundId = _getSoundRowsBySoundId;
    SoundManager.Instance.GetSoundRowsByKey = _getSoundRowsByKey;

    // 2. VoiceManager 델리게이트 연결
    VoiceManager.Instance.GetVoiceRow = _getVoiceRow;
    VoiceManager.Instance.GetAllVoiceRows = _getAllVoiceRows;
    VoiceManager.Instance.GetVoiceRowsByGroupKey = _getVoiceRowsByGroupKey;

    // NOTE: TbLoader 등록은 DomainTableRegistry (Generated)가 담당.
    // TB_SOUND._AfterLoad() / TB_VOICE._AfterLoad()가 호출되어
    // _OnAfterLoad()에서 어댑터 캐시 클리어 + 그룹 인덱스 빌드가 자동 수행됨.
}
```

### Adapter 패턴 (필수)

- Generated 클래스(PascalCase) → ISoundRow/IVoiceRow 인터페이스(snake_case) 변환
- `SoundRowAdapter`, `VoiceRowAdapter`로 변환
- Adapter 인스턴스는 캐시하여 중복 생성 방지

### _OnAfterLoad 구현 (Sound 전용)

`TB_SOUND`, `TB_VOICE`는 `_OnAfterLoad()`에서 다음을 수행한다:

- `BuildGroupIndices()` — sound_id/key 그룹 인덱스 빌드
- `SoundVoiceTableRegistry.ClearSoundAdapterCache()` / `ClearVoiceAdapterCache()` — 어댑터 캐시 초기화

> **Note**: AfterLoad hook 계약 자체는 `42-tablegen-implementation` 스킬이 SSOT.

---

## Phase 계획

### Phase 1 - 설계 확정 (완료)

- [x] Sound 도메인 설계 스킬 작성 (이 문서)
- [x] 기존 스킬(16/17/18) 정합성 업데이트
- [x] 샘플 코드 작성 (컴파일 유지)

### Phase 2 - 도메인 이관 (빌드 검증 PASS 후 완료)

- [x] 빌더에 AfterLoad 훅 추가 (`TB_*.{_AfterLoad, _OnAfterLoad}`)
- [x] `com.devian.domain.sound` UPM 패키지 신설
- [x] `SoundTable.xlsx` → `input/Sound/tables/` 이동
- [x] `input_common.json`에 Sound 도메인 추가
- [x] SoundManager/VoiceManager 소스 이동
- [x] SoundVoiceTableRegistry 이동
- [x] TB_SOUND/TB_VOICE partial 확장에 `_OnAfterLoad()` 구현
- [x] TbLoader 등록을 DomainTableRegistry (Generated)로 이관
- [x] asmdef 참조 정리
- [ ] **빌드 검증 (Generated/Registry 생성 확인 포함)**

> **Note**: Phase 2 완료 = 빌드 PASS + Generated 파일 생성 확인. 빌드가 통과하지 않으면 Phase 2는 미완료.

### Phase 3 - 정리

- [ ] Game 도메인에서 Sound 참조 제거 확인
- [ ] 불필요한 중복 코드 정리
- [ ] 문서 최종 업데이트

---

## 목표 DLL/asmdef 구조

```
com.devian.domain.sound/
├── Runtime/
│   ├── Devian.Domain.Sound.asmdef
│   ├── Generated/
│   │   ├── Sound.g.cs                 # TB_SOUND, TB_VOICE (Devian.Domain.Sound)
│   │   └── DomainTableRegistry.g.cs   # TbLoader 등록 + _AfterLoad 호출
│   ├── Tables/
│   │   ├── TB_SOUND.cs                # partial 확장 + _OnAfterLoad
│   │   ├── TB_VOICE.cs                # partial 확장 + _OnAfterLoad
│   │   └── SoundVoiceTableRegistry.cs # Manager 델리게이트 연결
│   └── Sound/
│       ├── SoundManager.cs            # namespace Devian
│       ├── VoiceManager.cs            # namespace Devian
│       ├── SoundChannel.cs
│       ├── SoundPlay.cs
│       └── SoundTypes.cs
└── Editor/
    └── Devian.Domain.Sound.Editor.asmdef
```

### asmdef 의존성

```
com.devian.domain.sound
└── com.devian.foundation

com.devian.domain.game
├── com.devian.foundation
└── com.devian.domain.sound  ← Game은 Sound에 의존 가능
```

---

## 사용 흐름 (Phase 2 완료 후)

> **Note**: TableManager 시그니처는 `14-table-manager` 스킬이 SSOT.

```csharp
// 1) TB 로드
yield return TableManager.Instance.LoadTablesAsync("table-ndjson", TableFormat.Json);

// 2) String 로드 (자막/로컬라이징)
yield return TableManager.Instance.LoadStringsAsync(
    "string-pb64",
    TableFormat.Pb64,
    SystemLanguage.Korean
);

// 3) Voice Resolve (언어별 캐시 생성)
VoiceManager.Instance.ResolveForLanguage(SystemLanguage.Korean);

// 4) 사운드 로드 (Voice 제외)
yield return SoundManager.Instance.LoadByKeyAsync("Common");

// 5) Voice 로드 (group_key 기반)
yield return VoiceManager.Instance.LoadByGroupKeyAsync(
    "greet",
    SystemLanguage.Korean,
    SystemLanguage.English
);

// 6) 재생
var sfxId = SoundManager.Instance.PlaySound("sfx_click");
var voiceId = VoiceManager.Instance.PlayVoice("hello");

// 7) 제어
SoundManager.Instance.StopSound(sfxId);
VoiceManager.Instance.StopVoice(voiceId);
```

---

## Verification (Phase 2 Done Definition)

Phase 2는 아래가 **모두 참**이어야 DONE이다:

1. `framework-cs/upm/com.devian.domain.sound/Runtime/Generated/`가 존재하고, `DomainTableRegistry.g.cs`가 존재한다.
2. `framework-cs/upm/com.devian.domain.game/Runtime/Generated/`에 `TB_SOUND*`, `TB_VOICE*` 생성물이 더 이상 존재하지 않는다.
3. repo 전체에서 `RegisterTbLoader("SOUND")`, `RegisterTbLoader("VOICE")` 호출은 **Generated DomainTableRegistry**에만 존재한다.
4. `input/build.sh input/input_common.json`가 성공한다. (npm ci 포함)

---

## Non-goals

- 테이블 컬럼 규약 → `16-sound-tables` 스킬
- 채널/풀/쿨타임/runtime_id 규약 → `17-sound-manager` 스킬
- Voice Resolve 정책 → `18-voice-table-resolve` 스킬

---

## See Also

범용 빌드/생성 규칙은 아래 스킬이 SSOT이다:

- `skills/devian-unity/30-unity-components/14-table-manager/SKILL.md` — **TbLoader SSOT / 중복 등록 금지** Hard Rule
- `skills/devian/42-tablegen-implementation/SKILL.md` — **AfterLoad hook 계약** Hard Rule
- `skills/devian/23-framework-ts-workspace/SKILL.md` — **npm ci / lock 동기화** 규약
- `skills/devian/22-generated-integration/SKILL.md` — **임시 stub 금지** 규약

Sound 도메인 관련:

- `skills/devian-unity/30-unity-components/16-sound-tables/SKILL.md` — TB_SOUND/TB_VOICE 테이블 규약
- `skills/devian-unity/30-unity-components/17-sound-manager/SKILL.md` — SoundManager 규약
- `skills/devian-unity/30-unity-components/18-voice-table-resolve/SKILL.md` — Voice Resolve 규약
- `skills/devian-common/20-domain/SKILL.md` — 도메인 아키텍처 규약
