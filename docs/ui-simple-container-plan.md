# UISimpleContainer 계획

---

## 1. 목적

`23-ui-package` 하위에 `21-container-simple`을 추가하고,
`UISimpleContainer`를 도입한다.

목표는 `UIScrollContainer`보다 훨씬 작은 책임만 가진
"가장 단순한 동작용 container 구현체"를 제공하는 것이다.

이 클래스는 다음 상황을 위한 기본형이다.

- `ScrollRect`가 전혀 필요 없는 UI subtree
- prefab에 배치된 `UIBaseFrame`들을 container lifecycle에만 연결하고 싶은 경우
- section layout / virtualization / logical row 개념이 필요 없는 경우

---

## 2. 기준

현재 최신 UIPackage 코드는 `Assets/Samples` 쪽이 앞서 있으므로,
구현 기준도 우선 이 경로를 따른다.

주요 기준 파일:

- `framework-cs/apps/UnityExample/Assets/Samples/Devian Foundation/0.1.0/UIPackage/Runtime/Container/UIBaseContainer.cs`
- `framework-cs/apps/UnityExample/Assets/Samples/Devian Foundation/0.1.0/UIPackage/Runtime/Container/UIBaseFrame.cs`
- `framework-cs/apps/UnityExample/Assets/Samples/Devian Foundation/0.1.0/UIPackage/Runtime/Container/UIScrollContainer.cs`

구현 완료 후 mirror sync 여부를 결정한다.

---

## 3. 핵심 정의

`UISimpleContainer`는 다음만 담당한다.

1. 자기 subtree 안의 `UIBaseFrame` 수집
2. 각 frame에 `canvas` 주입 (`frame._Init(canvas)`)
3. 각 frame의 init-complete 호출 (`frame._InitComplete()`)
4. 필요 시 frame 정리 (`frame._Clear()`)

즉:

- scroll owner 아님
- layout engine 아님
- virtualization engine 아님
- row/section model 없음

---

## 4. 지원 범위

### 포함

- static/non-scroll UI subtree
- `UIBaseFrame` 기반 하위 요소 초기화
- 최소한의 clear/cleanup
- `UICanvas` / `UIPanel.CreateContainer<T>()` 수명주기 편입

### 제외

- `ScrollRect` 구독
- viewport 계산
- logical row build
- grid virtualization
- runtime frame add/remove 자동 감지
- nested `UIBaseContainer` ownership 해결

---

## 5. 제약

### 5.1 Nested container 비지원

`UISimpleContainer`는 최소 구현을 목표로 하므로,
자기 subtree 안에 또 다른 `UIBaseContainer`를 두는 구조는 지원하지 않는다.

이유:

- frame ownership이 불명확해진다
- 최소 구현 범위를 넘는다
- `UIScrollContainer`처럼 별도 ownership 모델이 필요해진다

따라서 prefab 규칙은 다음으로 고정한다.

```text
UISimpleContainer
└── UIBaseFrame들만 배치
```

### 5.2 Frame 수집 범위

기본 구현은 `GetComponentsInChildren<UIBaseFrame>(true)`를 사용한다.
nested container를 허용하지 않는 전제에서만 안전하다.

---

## 6. 목표 API

가장 작은 public surface를 우선한다.

권장 시그니처:

```csharp
namespace Devian
{
    public class UISimpleContainer : UIBaseContainer
    {
        public bool IsInitialized { get; }
        public void Clear();
    }
}
```

설명:

- `IsInitialized`는 internal frame bootstrap 완료 여부
- `Clear()`는 active frame state 정리용
- `Refresh()` / `Rebuild()` / `ScrollTo()` 같은 API는 넣지 않는다

---

## 7. 목표 동작

### 7.1 onInit()

아무 일도 하지 않거나, 내부 list 초기화만 수행한다.

이 단계에서는 frame init을 하지 않는다.

### 7.2 onInitComplete()

여기서 실제 bootstrap을 수행한다.

1. `_frames.Clear()`
2. `GetComponentsInChildren<UIBaseFrame>(true)` 수집
3. 각 frame에 `frame._Init(canvas)` 호출
4. 각 frame에 `frame._InitComplete()` 호출
5. `_initialized = true`

이 순서는 현재 `UIScrollContainer`가 frame를 다루는 방식과 맞춘다.

### 7.3 Clear()

1. `_frames`를 순회하며 `frame._Clear()` 호출
2. `_frames.Clear()`
3. `_initialized = false`

### 7.4 OnDestroy()

`_initialized == true`이면 `Clear()` 호출.

---

## 8. 권장 구현 형태

```csharp
public class UISimpleContainer : UIBaseContainer
{
    public bool IsInitialized => _initialized;

    private readonly List<UIBaseFrame> _frames = new();
    private bool _initialized;

    protected override void onInitComplete()
    {
        _frames.Clear();
        _frames.AddRange(GetComponentsInChildren<UIBaseFrame>(true));

        foreach (var frame in _frames)
            frame._Init(canvas);

        foreach (var frame in _frames)
            frame._InitComplete();

        _initialized = true;
    }

    public void Clear()
    {
        foreach (var frame in _frames)
            frame._Clear();

        _frames.Clear();
        _initialized = false;
    }

    private void OnDestroy()
    {
        if (_initialized)
            Clear();
    }
}
```

이보다 더 복잡한 기능은 initial scope에서 제외한다.

---

## 9. 영향 파일

### Runtime

- `framework-cs/apps/UnityExample/Assets/Samples/Devian Foundation/0.1.0/UIPackage/Runtime/Container/UISimpleContainer.cs`

### Skills

- `skills/devian-unity/23-ui-package/21-container-simple/SKILL.md` 신규
- `skills/devian-unity/23-ui-package/SKILL.md` 인덱스 추가
- 필요 시 `skills/devian-unity/23-ui-package/11-ui-canvas-system/SKILL.md` 참조 문장 보강

### Mirror

필요 시 아래로 sync:

- `framework-cs/upm/com.devian.foundation/Samples~/UIPackage/`
- `framework-cs/apps/UnityExample/Packages/com.devian.foundation/Samples~/UIPackage/`

---

## 10. 스킬 문서 계획

신규 문서 디렉터리:

```text
skills/devian-unity/23-ui-package/21-container-simple/
└── SKILL.md
```

문서 핵심 내용:

- `UISimpleContainer`는 최소 container 구현체
- `UIBaseFrame` subtree bootstrap only
- no scroll / no virtualization / no layout engine
- nested container 비지원

인덱스 문서에는 다음 항목 추가:

| ID | 컴포넌트 | 설명 |
|----|----------|------|
| 21 | UISimpleContainer | 최소 container 구현체 (frame subtree bootstrap) |

---

## 11. 구현 순서

1. runtime에 `UISimpleContainer.cs` 추가
2. `UIBaseContainer` 기반 최소 구현 작성
3. Assets/Samples 기준 동작 확인
4. `21-container-simple/SKILL.md` 작성
5. `23-ui-package/SKILL.md`에 21 항목 추가
6. 필요 시 `11-ui-canvas-system/SKILL.md` 참조 보강
7. mirror sync 여부 결정

---

## 12. 검증 항목

### Runtime

- `UICanvas.Init()` 시 `UISimpleContainer`가 자동 수집된다
- `UISimpleContainer.onInitComplete()`에서 child `UIBaseFrame`가 초기화된다
- `UIPanel.CreateContainer<UISimpleContainer>()`로 동적 생성 시에도 bootstrap 된다
- `Clear()` 호출 시 child `frame._Clear()`가 실행된다

### Structure

- `UISimpleContainer`는 `ScrollRect` 의존성이 없다
- `IUIScrollSection` 의존성이 없다
- public API가 과도하게 커지지 않는다

### Docs

- `21-container-simple/SKILL.md` 신규 생성
- `23-ui-package/SKILL.md` 인덱스 반영
- `git diff --check` 통과

---

## 13. 오픈 포인트

### 13.1 Refresh 필요 여부

현재 목표는 최소 구현이므로 `Refresh()`는 넣지 않는다.
필요해지면 이후 별도 요구로 추가한다.

### 13.2 Nested container 지원

초기 버전에서는 지원하지 않는다.
이 요구가 생기면 ownership 규칙부터 다시 설계해야 한다.

### 13.3 Frame 수집 root

기본은 `this.transform` subtree 전체다.
별도 `ContentRoot` 필드는 initial scope에서 제외한다.

