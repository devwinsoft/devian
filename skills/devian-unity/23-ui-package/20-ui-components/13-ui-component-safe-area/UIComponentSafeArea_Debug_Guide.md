# UIComponentSafeArea Offset 모드 버그 디버깅 지시서

## 목적

Offset 모드 + Apply Top 설정에서 element가 "화면 위로 빠져 나오고 중점이 top에 붙는" 버그의 원인을 Unity MCP를 통해 확인한다.

---

## 환경 조건

- CanvasScaler: Scale With Screen Size, Match = height (1)
- UIComponentSafeArea: Offset 모드, `_applyTop = true` (나머지는 사용자가 설정한 대로)
- `_target = null` (self), 좌우 stretch + 세로 fixed
- Editor Simulation: iPhone 14 Pro

---

## Step 1: 적용 전 baseline 상태 확인

Refresh가 실행되기 **전에** 다음 값들을 기록한다.

```
대상: UIComponentSafeArea가 붙은 RectTransform (target = self)

기록할 값:
1. anchorMin (x, y)
2. anchorMax (x, y)
3. offsetMin (x, y)
4. offsetMax (x, y)
5. pivot (x, y)
6. rect (width, height)
```

Unity MCP로 읽는 방법:

```csharp
var sa = FindObjectOfType<Devian.UIComponentSafeArea>();
var rt = sa.transform as RectTransform;
Debug.Log($"[BEFORE] anchorMin={rt.anchorMin} anchorMax={rt.anchorMax}");
Debug.Log($"[BEFORE] offsetMin={rt.offsetMin} offsetMax={rt.offsetMax}");
Debug.Log($"[BEFORE] pivot={rt.pivot} rect={rt.rect}");
```

---

## Step 2: Canvas / CanvasScaler 상태 확인

```csharp
var canvas = sa.GetComponentInParent<Canvas>();
Debug.Log($"[CANVAS] renderMode={canvas.renderMode} scaleFactor={canvas.scaleFactor}");
Debug.Log($"[CANVAS] referenceResolution={canvas.GetComponent<UnityEngine.UI.CanvasScaler>()?.referenceResolution}");
Debug.Log($"[CANVAS] matchWidthOrHeight={canvas.GetComponent<UnityEngine.UI.CanvasScaler>()?.matchWidthOrHeight}");
Debug.Log($"[SCREEN] width={Screen.width} height={Screen.height} safeArea={Screen.safeArea}");
Debug.Log($"[SCREEN] orientation={Screen.orientation}");
```

기록할 핵심값:
- `canvas.scaleFactor` — 이 값이 1이 아니면 Offset 모드에 좌표계 불일치가 발생할 수 있음
- `Screen.width / height` vs `referenceResolution` — 비율 확인

---

## Step 3: Refresh 실행 후 상태 확인

```csharp
sa.Refresh();
var rt = sa.transform as RectTransform;
Debug.Log($"[AFTER] anchorMin={rt.anchorMin} anchorMax={rt.anchorMax}");
Debug.Log($"[AFTER] offsetMin={rt.offsetMin} offsetMax={rt.offsetMax}");
Debug.Log($"[AFTER] pivot={rt.pivot} rect={rt.rect}");
Debug.Log($"[AFTER] IsApplied={sa.IsApplied} LastAppliedSafeArea={sa.LastAppliedSafeArea}");
```

---

## Step 4: 핵심 수치 계산 및 비교

위 값들을 모아서 아래 표를 채운다:

```
A. Screen.height               = ____
B. canvas.scaleFactor           = ____
C. referenceResolution.y        = ____
D. LastAppliedSafeArea.yMax     = ____
E. topInset (screen px)         = A - D = ____
F. topInset (canvas unit, 올바른 값) = E / B = ____
G. offsetMax.y 변화량 (실제)    = AFTER.offsetMax.y - BEFORE.offsetMax.y = ____
H. offsetMin.y 변화량 (실제)    = AFTER.offsetMin.y - BEFORE.offsetMin.y = ____
```

### 검증 포인트

| 검증 | 조건 | 의미 |
|------|------|------|
| G == -E 인가? | G가 -59 같은 screen pixel 값이면 | 코드가 screen px를 canvas unit에 직접 적용 중 (좌표계 불일치) |
| G == -F 인가? | G가 -F와 같으면 | 좌표계 변환이 올바름 |
| B != 1 인가? | scaleFactor가 1이 아니면 | Offset 모드에 좌표계 버그가 활성화됨 |
| H == G 인가? | 둘이 같으면 | fixed axis 경로 (shift, 크기 유지) |
| H != G 인가? | 둘이 다르면 | non-fixed axis 경로 (shrink) |

---

## Step 5: "위로 빠져 나오는" 원인 특정

AFTER 값에서 element의 실제 screen 위치를 계산한다:

```
parentHeight (canvas units) = parent RectTransform의 rect.height
anchorY_bottom = anchorMin.y * parentHeight
anchorY_top    = anchorMax.y * parentHeight

element_bottom = anchorY_bottom + offsetMin.y
element_top    = anchorY_top + offsetMax.y
element_center = (element_bottom + element_top) / 2

canvas_height  = root canvas의 rect.height (= referenceResolution.y with Match=1)
```

```csharp
var parent = rt.parent as RectTransform;
Debug.Log($"[PARENT] rect={parent.rect} anchorMin={parent.anchorMin} anchorMax={parent.anchorMax}");
Debug.Log($"[ROOT] canvas rect={canvas.GetComponent<RectTransform>().rect}");
```

### 판정

- `element_top > canvas_height` → element가 화면 위로 넘침
- `element_center > canvas_height * 0.9` → 중점이 top 근처에 위치
- `element_bottom < 0` → element가 화면 아래로 넘침

---

## Step 6: Anchor 모드와 비교

같은 조건에서 `_applyMode`만 `Anchor`로 바꿔서 Refresh한 뒤, 동일한 값들을 기록한다. Anchor 모드가 정상 동작하는지 확인하고, Offset 모드와의 차이를 비교한다.

---

## 의심 원인 후보 (우선순위 순)

### 후보 1: CanvasScaler 좌표계 불일치

Offset 모드에서 `topInset`이 **screen pixel** 단위로 계산되지만 `offsetMax.y -= topInset`으로 **canvas unit**에 직접 적용된다. `canvas.scaleFactor != 1`이면 두 단위가 불일치한다.

확인: Step 4에서 `G == -E` (screen px 직적용) vs `G == -F` (canvas unit 변환됨) 비교.

### 후보 2: Baseline 캡처 시점 오염

`[ExecuteAlways]` + Editor Simulation에서 이미 safe area가 적용된 상태의 offset이 baseline으로 캡처되어, Refresh 때 이중 적용되는 경우.

확인: Step 1의 BEFORE 값이 "순수 원본"인지, 이미 변형된 값인지 확인. 특히 `offsetMax.y`가 이미 음수면 의심.

### 후보 3: fixed axis 판정 오류

`Mathf.Approximately(anchorMin.y, anchorMax.y)` 결과가 예상과 다른 경우. 세로 stretch인데 fixed로 판정되거나 그 반대.

확인: Step 1의 `anchorMin.y`와 `anchorMax.y`가 정확히 같은지, 아니면 미세하게 다른지 확인.

---

## 결과 보고

위 Step 1~5의 모든 로그 출력 값과 Step 4 표를 채워서 보고한다. 어떤 후보가 원인인지 판단할 수 있다.
