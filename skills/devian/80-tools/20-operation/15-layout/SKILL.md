# 20-operation/15-layout — 레이아웃


Status: ACTIVE
AppliesTo: v10


## Purpose

Operation 웹앱의 전체 레이아웃과 스타일 가이드를 정의한다.


---


## Constraints

- 바닐라 TS + 경량 라이브러리 (프레임워크 없음)
- localhost 전용 -> 반응형 불필요 (데스크톱 해상도 고정)


---


## Layout

```
┌──────────────────────────────────────────────────┐
│  Operation                                       │
├──────────────┬───────────────┬───────────┤
│ Obfuscate    │ Deobfuscate   │ Save Data │
├──────────────┴───────────────┴───────────┤
│  Pipeline: (탭별 파이프라인 설명)                    │
├──────────────────────────────────────────────────┤
│                                                  │
│  (탭별 콘텐츠 영역)                                │
│                                                  │
└──────────────────────────────────────────────────┘
```

- 상단: 앱 타이틀
- 탭 바: Obfuscate / Deobfuscate / Save Data
- 파이프라인 설명: 현재 탭의 encode/decode 파이프라인 표시
- 콘텐츠 영역: 탭별 UI


---


## Style Guide

| 항목 | 선택 |
|------|------|
| 스타일 방식 | 순수 CSS |
| 테마 | 다크 테마 |
| JSON 에디터 | CodeMirror 6 |


---


## Related

- [00-overview](../00-overview/SKILL.md) — Operation 개요
- [01-policy](../01-policy/SKILL.md) — 정책
- [10-app-shell](../10-app-shell/SKILL.md) — 앱 셸
- [16-page-obfuscate](../16-page-obfuscate/SKILL.md) — Obfuscate 탭 UI
- [17-page-deobfuscate](../17-page-deobfuscate/SKILL.md) — Deobfuscate 탭 UI
- [18-page-savedata](../18-page-savedata/SKILL.md) — Save Data 탭 UI
