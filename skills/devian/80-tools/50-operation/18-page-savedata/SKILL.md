# 50-operation/18-page-savedata — Save Data 탭


Status: ACTIVE
AppliesTo: v10


## UI

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


## Related

- [00-overview](../00-overview/SKILL.md) — Operation 개요
- [15-layout](../15-layout/SKILL.md) — 레이아웃, 스타일 가이드
