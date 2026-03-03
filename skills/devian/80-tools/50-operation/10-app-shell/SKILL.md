# 50-operation/10-app-shell — 앱 셸


Status: ACTIVE
AppliesTo: v10


## Purpose

Operation 웹앱의 프로젝트 구조, 빌드 설정, 탭 네비게이션을 정의한다.


---


## Build

- 빌드 도구: **Vite**
- dev: `npm run dev` → `vite` (localhost dev server, HMR)
- `framework-ts/` 워크스페이스 앱으로 등록


## Project Location

```
framework-ts/apps/Operation/
```


## Project Structure

```
framework-ts/apps/Operation/
├── index.html
├── package.json
├── tsconfig.json
├── vite.config.ts
└── src/
    ├── main.ts                  ← 엔트리: 탭 초기화, 라우팅
    ├── style.css                ← 전역 스타일
    ├── tabs/
    │   ├── obfuscate.ts         ← Obfuscate 탭
    │   ├── deobfuscate.ts       ← Deobfuscate 탭
    │   └── savedata.ts          ← Save Data 탭
    └── codec/
        ├── dvn-codec.ts         ← DVN 파이프라인 (GZip → AES → Base64)
        └── obfuscation-codec.ts ← ComplexUtil 난독화
```


---


## Navigation

- **단일 페이지** + **탭 전환**으로 기능을 구분한다.
- 탭 목록: Obfuscate / Deobfuscate / Save Data
- 탭 정의는 [03-ssot](../03-ssot/SKILL.md) 참조.


---


## Related

- [00-overview](../00-overview/SKILL.md) — Operation 개요
- [01-policy](../01-policy/SKILL.md) — 정책
- [Tools SSOT](../../03-ssot/SKILL.md) §TypeScript Workspace 정본
