# 22-effect-manager

Status: ACTIVE
AppliesTo: v10
Type: Component Specification

## 1. 목적

Unity에서 사용할 Effect 시스템을 정의한다.

- 스폰/디스폰은 `BundlePool` 기반 (Addressables 캐시에서 prefab 조회)
- 효과 구현은 prefab 내부 컴포넌트(IEffectRunner)로 확장
- EffectObject는 상태(Play/Pause/Resume/Stop/Remove) 및 자동 반환을 관리

---

## 2. 네임스페이스

모든 코드는 `namespace Devian` 고정.

---

## 3. 생성 대상 패키지

- `com.devian.foundation`

---

## 4. 파일 위치 (정본)

```
com.devian.foundation/Runtime/Unity/Effects/
├── EFFECT_ID.cs
├── EffectManager.cs
├── EffectObject.cs
├── IEffectRunner.cs
└── Runners/
    ├── ParticleEffectRunner.cs
    └── AnimEffectRunner.cs
```

Editor:

```
com.devian.foundation/Editor/AssetId/Generated/
└── EFFECT_ID.Editor.cs
```

---

## 5. EFFECT_ID 규약

- string wrapper 타입
- `Value` 필드 + `IsValid` 제공
- implicit operator string / EFFECT_ID 제공

---

## 6. EffectManager 규약

- Singleton은 `AutoSingleton<EffectManager>` 사용 (자동 생성형)
- 외부 API는 `CreateEffect(...)` / `Remove(...)` 제공
- Pooling은 내부적으로 `BundlePool.Spawn<EffectObject>(effectId.Value, ...)` / `BundlePool.Despawn(effectObject)` 사용

Attach 타입:

```csharp
public enum EFFECT_ATTACH_TYPE
{
    World,
    Ground,
    Child,
}
```

Ground 규칙:

- effect.position + (0,100,0)에서 아래로 SphereCast(radius=0.01)로 지면을 찾으면 hit.point로 이동
- Ground인 경우 parent는 Root(EffectManager.transform)로 고정

World 규칙:

- parent는 Root로 고정

Child 규칙:

- parent는 attachTr 유지

---

## 7. EffectObject 규약

- MonoBehaviour, IPoolable<EffectObject> 구현
- 상태 머신: Playing / Paused / Stopped / Removed
- 공통 API: Init(), Play(), Pause(), Resume(), Stop(), Remove(), Clear()
- SetSortingOrder(int) / SetDirection(bool) 제공

Remove 규칙:

- `BundlePool.Despawn(this)`로 반환

---

## 8. IEffectRunner 규약

EffectObject 내부에서만 호출하는 public API이므로 메서드명은 `_` prefix를 사용한다.

필수 메서드:

- `_OnEffectAwake(EffectObject owner)`
- `_OnEffectPlay()`
- `_OnEffectPause()`
- `_OnEffectResume()`
- `_OnEffectStop()`
- `_OnEffectLateUpdate()`
- `_OnEffectClear()`
- `_SetSortingOrder(int order)` (optional 구현 가능)

---

## 9. 제공 Runner 2종

### ParticleEffectRunner

- 자식 ParticleSystem 전부를 관리
- playTime > 0이면 시간 종료 후 Stop
- playTime == 0이면 모든 파티클이 죽으면 Stop
- Stop 시 StopEmitting 후 fadeOutTime 대기 후 Remove

### AnimEffectRunner

- `[RequireComponent(typeof(AnimSequencePlayer))]` 로 AnimSequencePlayer 의존성 강제
- Runner 자체는 AnimSequenceData나 AnimationClip을 보유하지 않음
- AnimSequencePlayer.PlayDefault()만 호출하여 재생
- playSpeed 필드: PlayDefault(playSpeed, callback)으로 전달
- ComputedPlayTime 제공 (초 단위):
  - AnimSequencePlayer._GetDefaultPlayTime(playSpeed) 호출
  - 무한이면 -1
  - clip/sequence가 없으면 0
  - clip/sequence가 있으면 계산값
- 자동 제거 정책:
  - ComputedPlayTime == -1이면 자동 Remove 하지 않음
  - 그 외에는 시퀀스 완료 콜백으로 Remove
- Stop은 즉시 Remove

---

## 10. Hard Rules

- Spawn/Despawn은 BundlePool로만 한다.
- ID는 prefab.name 그대로 사용한다.
- 네임스페이스/폴더 규약 위반 금지.
