# Devian v10 — Glossary

Status: ACTIVE
AppliesTo: v10
SSOT: skills/devian/10-module/03-ssot/SKILL.md

## Purpose

Devian 스킬 문서 전반에서 사용되는 핵심 용어와 플레이스홀더를 정의한다.
각 항목은 1줄 요약만 제공하며, 정확한 정의는 SSOT를 참조한다.

---

## Core Terms

| 용어 | 설명 |
|------|------|
| **DomainType** | `DATA` 또는 `PROTOCOL`. 빌드 대상의 종류. |
| **DomainKey** | DATA 도메인의 고유 키 (예: `Common`, `Game`). |
| **ProtocolGroup** | PROTOCOL 그룹명 (예: `Game`). C# 프로젝트명/TS 폴더명에 사용. |
| **ProtocolName** | Protocol JSON 파일의 base name (예: `C2Game`). |
| **tempDir** | 빌드 중간 산출물 경로 (staging). |
| **staging** | tempDir 아래 중간 생성물. final 복사 전 상태. |
| **final** | 최종 배포 경로. tableConfig/csConfig/tsConfig로 결정. |
| **Generated** | 빌더가 생성하는 코드/데이터. 수동 편집 금지. |
| **Generated Only** | 빌더가 전체 파일을 덮어쓰는 모드. |

---

## Input JSON Files

### `{buildInputJson}`

빌드 입력 JSON 파일 경로.

- 예: `input/input_common.json`
- 포함 키: `tempDir`, `csConfig`, `tsConfig`, `upmConfig`, `domains`, `protocols`
- 상대 경로 해석 기준: 이 파일의 위치

### `{projectConfigJson}`

프로젝트 설정 JSON 파일 경로.

- 예: `input/config.json`
- 포함 키: `tableConfig` (tableDirs, stringDirs, soundDirs)

---

## Placeholders

| 플레이스홀더 | 설명 |
|-------------|------|
| `{tempDir}` | 중간 산출물 루트 |
| `{DomainKey}` | DATA 도메인 키 |
| `{ProtocolGroup}` | Protocol 그룹명 |
| `{ProtocolName}` | Protocol 파일 base name |
| `{csConfig.moduleDir}` | C# 수동 모듈 루트 |
| `{csConfig.generateDir}` | C# 생성 모듈 루트 |
| `{tsConfig.moduleDir}` | TS 수동 모듈 루트 |
| `{tsConfig.generateDir}` | TS 생성 모듈 루트 |
| `{tableConfig.tableDirs}` | 테이블 출력 경로 배열 |
| `{tableConfig.stringDirs}` | 스트링 테이블 출력 경로 배열 |
| `{tableConfig.soundDirs}` | 사운드 출력 경로 배열 |
| `{tableDir}` | tableDirs 배열의 개별 요소 |
| `{stringDir}` | stringDirs 배열의 개별 요소 |
| `{soundDir}` | soundDirs 배열의 개별 요소 |
| `{upmConfig.sourceDir}` | UPM 소스 루트 |
| `{upmConfig.packageDir}` | UPM 패키지 출력 루트 |
| `{buildInputJson}` | 빌드 입력 JSON 경로 |
| `{projectConfigJson}` | 프로젝트 설정 JSON 경로 |

---

## Canonical Definitions

정확한 정의, 금지 키, 머지 규칙의 정본:

→ `skills/devian/10-module/03-ssot/SKILL.md`

---

## Reference

- Index: `skills/devian/SKILL.md`
- SSOT: `skills/devian/10-module/03-ssot/SKILL.md`
