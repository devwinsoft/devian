# UICanvasFrame → UIPanel rename 계획

---

## 1. 변경 요약

| 기존 | 변경 |
|------|------|
| `UICanvasFrame` (비제네릭) | **`UIPanel`** |
| `UICanvasFrame<TCanvas>` (제네릭) | **`UIPanel<TCanvas>`** |
| `UICanvasFrame.cs` (파일) | **`UIPanel.cs`** |

---

## 2. 코드 영향

### 파일 이름 변경

```
UICanvasFrame.cs → UIPanel.cs  (3-path mirror × 1 = 3개)
```

### 코드 내 참조

| 파일 | 참조 |
|------|------|
| `UIPanel.cs` (구 UICanvasFrame.cs) | 클래스명 `UICanvasFrame` → `UIPanel`, `UICanvasFrame<TCanvas>` → `UIPanel<TCanvas>` |
| `UICanvas.cs` | `List<UICanvasFrame>` → `List<UIPanel>`, `GetComponentsInChildren<UICanvasFrame>` → `GetComponentsInChildren<UIPanel>`, `GetComponent<UICanvasFrame>` → `GetComponent<UIPanel>` |
| `UIContainerBase.cs` | `UICanvasFrame` 참조 있으면 변경 |
| `UIGameFrameBag.cs` | `UICanvasFrame<UIGameCanvas>` → `UIPanel<UIGameCanvas>` |

### 스킬 문서

| 파일 | 변경 |
|------|------|
| `11-ui-canvas-system/SKILL.md` | 클래스명, 파일명, API 시그니처 전부 |
| `22-ui-container-scroll-view/SKILL.md` | Prefab Hierarchy |
| `23-ui-package/SKILL.md` | 주요 파일 목록, Components 테이블 |
| `10-ui-manager/SKILL.md` | Reference 링크 |
| `devian/00-overview/SKILL.md` | 키워드 테이블 |

---

## 3. 구현 순서

| 단계 | 작업 |
|------|------|
| 1 | UPM: `UICanvasFrame.cs` → `UIPanel.cs` 파일 생성 + 내용 치환 |
| 2 | `UICanvas.cs` 내 참조 치환 |
| 3 | `UIContainerBase.cs` 내 참조 치환 (있으면) |
| 4 | `UIGameFrameBag.cs` 치환 |
| 5 | 구 파일 삭제 (3-path) |
| 6 | 3-path mirror sync |
| 7 | 스킬 문서 갱신 |
