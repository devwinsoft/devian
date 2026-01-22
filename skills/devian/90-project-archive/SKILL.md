# Devian v10 — Project Archive

Status: ACTIVE  
AppliesTo: v10  
SSOT: skills/devian/03-ssot/SKILL.md

## Purpose

Devian 프로젝트 전체를 배포/백업용 zip 파일로 아카이브하는 규칙을 정의한다.

---

## Archive Rules (정책)

### 포함 대상

- 소스코드 (`framework-cs/`, `framework-ts/`)
- 스킬 문서 (`skills/`)
- 입력 파일 (`input/`)
- 설정 파일 (`input_common.json` 등)
- 생성 코드 (`**/generated/`)
- 출력 데이터 (`output/`)

### 제외 대상

1. `.gitignore`에 명시된 패턴
2. 내장 제외 목록:
   - `.git/`
   - `node_modules/`
   - `__pycache__/`
   - `*.pyc`
   - `.DS_Store`
   - `temp/` (input_common.json의 tempDir)

---

## Usage

### Python 버전

```bash
python skills/devian/90-project-archive/scripts/archive_project.py [options]
```

### Node.js 버전

```bash
node framework-ts/tools/archive/archive.js [options]
```

### Options

| 옵션 | 설명 | 기본값 |
|------|------|--------|
| `--root` | 프로젝트 루트 경로 | 자동 탐지 |
| `--output` | 출력 디렉토리 | 프로젝트 루트 |
| `--exclude-generated` | generated 폴더 제외 | 포함 |
| `--exclude-data` | ndjson 데이터 제외 | 포함 |
| `--include-temp` | temp 폴더 포함 | 제외 |

### Examples

```bash
# Python - 기본 사용
python skills/devian/90-project-archive/scripts/archive_project.py

# Node.js - 기본 사용
node framework-ts/tools/archive/archive.js

# 출력 경로 지정
node framework-ts/tools/archive/archive.js --output /path/to/backup

# generated 제외
node framework-ts/tools/archive/archive.js --exclude-generated
```

---

## Output

- **파일명**: `devian-YYYYMMDD-HHMMSS.zip`
- **압축**: 빠른 압축 (ZIP_DEFLATED, level=1)
- **구조**: 프로젝트 상대 경로 보존

---

## Implementation

| 버전 | 위치 | 의존성 |
|------|------|--------|
| Python | `skills/devian/90-project-archive/scripts/archive_project.py` | 없음 (표준 라이브러리) |
| Node.js | `framework-ts/tools/archive/archive.js` | 없음 (시스템 zip 또는 PowerShell) |

---

## Reference

- Policy SSOT: `skills/devian/03-ssot/SKILL.md`
