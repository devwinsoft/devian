# Devian v10 — Project Archive

Status: ACTIVE
AppliesTo: v10
SSOT: skills/devian-core/03-ssot/SKILL.md

## Purpose

Devian 프로젝트 전체를 배포/백업용 zip 파일로 아카이브하는 규칙을 정의한다.

---

## Archive Rules (정책)

### 포함 대상

- 소스코드 (`framework-cs/`, `framework-ts/`)
- 스킬 문서 (`skills/`)
- 입력 파일 (`input/`)
- 설정 파일 (`{buildInputJson}` 등)
- 생성 코드 (`**/Generated/`)
- 출력 데이터 (`output/`)

### 제외 대상

1. `.gitignore`에 명시된 패턴
2. 내장 제외 목록:
   - `.git/`
   - `node_modules/`
   - `.DS_Store`
   - `{buildInputJson}.tempDir` (기본: `temp/`)

---

## Archive Tool (정본)

**유일한 아카이브 스크립트:**

```
framework-ts/tools/scripts/archive.js
```

### 루트 자동 기준

우선순위:
1. `${root}/input/input_common.json`
2. `${root}/input_common.json`
3. `.git` 폴더 존재
4. `skills` 폴더 존재

### temp 제외 기준

`{buildInputJson}.tempDir` 값을 읽어 해당 폴더를 제외한다:

1. `{buildInputJson}` 파일에서 `tempDir` 키 값을 읽음 (기본값: `"temp"`)
2. `tempDir`는 `{buildInputJson}` 디렉토리 기준 상대경로로 해석
3. 해석된 경로를 프로젝트 루트 기준 상대경로로 변환
4. 해당 경로와 하위 모든 파일을 제외

**예시:**
- `{buildInputJson}`: `input/input_common.json`
- `tempDir`: `"temp"` (기본값)
- 해석: `input/temp` → 제외 패턴: `input/temp`, `input/temp/**`

### Generated 제외

`--exclude-generated` 옵션 사용 시 `Generated` 폴더를 제외한다 (대문자 G).

---

## Usage

```bash
node framework-ts/tools/scripts/archive.js [options]
```

### Options

| 옵션 | 설명 | 기본값 |
|------|------|--------|
| `--root` | 프로젝트 루트 경로 | 자동 탐지 |
| `--output` | 출력 디렉토리 | 프로젝트 루트 |
| `--exclude-generated` | Generated 폴더 제외 | 포함 |
| `--exclude-data` | ndjson 데이터 제외 | 포함 |
| `--include-temp` | temp 폴더 포함 | 제외 |

### Examples

```bash
# 기본 사용
node framework-ts/tools/scripts/archive.js

# 출력 경로 지정
node framework-ts/tools/scripts/archive.js --output /path/to/backup

# Generated 제외
node framework-ts/tools/scripts/archive.js --exclude-generated
```

---

## Output

- **파일명**: `devian-YYYYMMDD-HHMMSS.zip`
- **압축**: 빠른 압축 (ZIP_DEFLATED, level=1)
- **구조**: 프로젝트 상대 경로 보존

---

## Implementation

| 위치 | 의존성 |
|------|--------|
| `framework-ts/tools/scripts/archive.js` | 없음 (시스템 zip 또는 PowerShell) |

---

## Reference

- Policy SSOT: `skills/devian-core/03-ssot/SKILL.md`
- Tools SSOT: `skills/devian-tools/03-ssot/SKILL.md`
