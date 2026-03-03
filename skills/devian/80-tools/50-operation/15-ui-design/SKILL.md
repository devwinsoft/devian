# 50-operation/15-ui-design — UI 디자인


Status: ACTIVE
AppliesTo: v10


## Purpose

Operation 웹앱의 UI 디자인을 정의한다.
레이아웃, 탭별 콘텐츠 구성, 스타일 가이드를 다룬다.


---


## Constraints

- 바닐라 TS + 경량 라이브러리 (프레임워크 없음)
- localhost 전용 → 반응형 불필요 (데스크톱 해상도 고정)


---


## Layout

```
┌──────────────────────────────────────────────────┐
│  Operation                                       │
├──────────────┬───────────────┬───────────────────┤
│  Obfuscate   │  Deobfuscate  │  Save Data        │
├──────────────┴───────────────┴───────────────────┤
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


## Obfuscate 탭

```
Pipeline: byte-substitution obfuscation

┌──────────────────────────────────────────────────┐
│  Input (텍스트 영역)                              │
│                                                  │
├──────────────────────────────────────────────────┤
│                              [Obfuscate]         │
├──────────────────────────────────────────────────┤
│  Output (텍스트 영역, 읽기 전용)                   │
│                                                  │
└──────────────────────────────────────────────────┘
```


## Deobfuscate 탭

```
Pipeline: byte-substitution deobfuscation

┌──────────────────────────────────────────────────┐
│  Input (텍스트 영역)                              │
│                                                  │
├──────────────────────────────────────────────────┤
│                              [Deobfuscate]       │
├──────────────────────────────────────────────────┤
│  Output (텍스트 영역, 읽기 전용)                   │
│                                                  │
└──────────────────────────────────────────────────┘
```


## Save Data 탭

```
┌──────────────────────────────────────────────────┐
│  파일 업로드 (.json 또는 .dvn)                     │
│                                     [Import]     │
├──────────────────────────────────────────────────┤
│  Wrapper JSON (payload 제외)                      │
│  ┌──────────────────────────────────────────┐    │
│  │  JSON Editor 1                           │    │
│  └──────────────────────────────────────────┘    │
│                                                  │
│  Payload JSON (게임 상태)                         │
│  ┌──────────────────────────────────────────┐    │
│  │  JSON Editor 2                           │    │
│  └──────────────────────────────────────────┘    │
├──────────────────────────────────────────────────┤
│              [Export & Download .json/.dvn]       │
└──────────────────────────────────────────────────┘
```

파일 확장자 기반 자동 분기 (`.json` 또는 `.dvn`):

| 항목 | `.json` (Editor) | `.dvn` (Mobile) |
|------|-----------------|-----------------|
| Import | `JSON.parse` → payload 분리/deobfuscate | `decodeDvn` → `JSON.parse` → payload 분리/deobfuscate |
| Export | payload obfuscate → wrapper 합성 → `.json` | payload obfuscate → wrapper 합성 → `encodeDvn` → `.dvn` |

2-level decode (두 확장자 공통):
- wrapper JSON에서 payload 필드 추출 → ComplexUtil deobfuscate → Editor 2에 표시
- Editor 1에는 payload를 제외한 wrapper JSON을 표시

워크플로우:
1. `.json` 또는 `.dvn` 파일 업로드 → [Import] → wrapper는 Editor 1에, payload 해독 결과는 Editor 2에
2. 각 에디터에서 JSON 편집
3. [Export & Download] → payload 난독화 → wrapper에 삽입 → 원본과 동일 확장자로 다운로드


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
