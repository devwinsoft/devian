# Runtime 폴더 재구조화 계획

---

## 변경

```
Runtime/Container/  (변경 전 — flat)
├── UIBaseContainer.cs
├── UIBaseFrame.cs
├── IUIScrollSection.cs
├── UIScrollContainer.cs
├── UIScrollRowLayout.cs
├── UIScrollSectionLayout.cs
├── UISimpleFrame.cs
├── UIGridFrame.cs
└── UIGridCell.cs

Runtime/ (변경 후)
├── Container/
│   ├── UIBaseContainer.cs
│   └── UIBaseFrame.cs
├── Scroll/
│   ├── IUIScrollSection.cs
│   ├── UIScrollContainer.cs
│   ├── UIScrollRowLayout.cs
│   ├── UIScrollSectionLayout.cs
│   └── UISimpleFrame.cs
└── Grid/
    ├── UIGridFrame.cs
    └── UIGridCell.cs
```

## 영향

### 코드
- 파일 이동만. 클래스명/namespace 변경 없음.
- SSOT 주석 경로 갱신 필요.

### 스킬
- 22-ui-container-scroll: Code Path 갱신 (Container/ → Scroll/)
- 23-ui-frame-grid: Code Path 갱신 (Container/ → Grid/)
- 24-ui-frame-simple: Code Path 갱신 (Container/ → Scroll/)
- 11-ui-canvas-system: Code Path에 Container/ 유지 (base만)

### 3-path mirror
- 구 경로 파일 삭제 + 새 경로 파일 복사

## 구현 순서

| 단계 | 작업 |
|------|------|
| 1 | UPM: Runtime/Scroll, Runtime/Grid 폴더 생성 |
| 2 | UPM: 파일 이동 |
| 3 | UPM: SSOT 주석 경로 갱신 |
| 4 | 3-path: 구 파일 삭제 + 새 파일 복사 |
| 5 | 스킬 Code Path 갱신 |
