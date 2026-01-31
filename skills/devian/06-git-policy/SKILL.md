# Devian v10 — Git Policy

Status: ACTIVE
AppliesTo: v10
SSOT: this file

## Purpose

**Devian 프로젝트의 Git 사용 정책**을 정의한다.

줄바꿈 문자, 파일 권한, 커밋 규칙 등 Git 관련 표준을 통일한다.

---

## 1. 줄바꿈 문자 (Line Endings) — Hard Rule

### 정책

| 파일 유형 | 줄바꿈 | 설명 |
|----------|--------|------|
| Shell 스크립트 (`*.sh`) | **LF** | CRLF 사용시 shebang 오류 발생 |
| 모든 텍스트 파일 | **LF** | 플랫폼 간 일관성 유지 |

### .gitattributes 필수 설정

```gitattributes
# 기본: 모든 텍스트 파일 LF
* text=auto eol=lf

# Shell 스크립트: 반드시 LF
*.sh text eol=lf

# 바이너리 파일
*.png binary
*.jpg binary
*.dll binary
*.exe binary
```

### Git Config 권장 설정

```bash
# 커밋시 CRLF → LF 자동 변환
git config core.autocrlf input
```

---

## 2. CRLF 문제 진단 및 해결

### 증상

```bash
./build.sh
# -bash: ./build.sh: cannot execute: required file not found
```

**원인**: `#!/bin/bash\r` (CRLF) → bash가 `/bin/bash\r`를 찾으려 시도

### 진단

```bash
file script.sh
# CRLF인 경우: "with CRLF line terminators" 표시
```

### 해결

```bash
# sed로 CRLF → LF 변환
sed -i 's/\r$//' script.sh

# 또는 dos2unix 사용
dos2unix script.sh
```

---

## 3. 커밋 메시지 규칙

### 형식

```
<type>: <subject>

<body (optional)>
```

### Type 목록

| Type | 설명 |
|------|------|
| `feat` | 새 기능 |
| `fix` | 버그 수정 |
| `refactor` | 리팩토링 (기능 변경 없음) |
| `docs` | 문서 변경 |
| `chore` | 빌드, 설정 변경 |
| `test` | 테스트 추가/수정 |

### 예시

```
feat: Add WebSocket reconnection logic

- Implement exponential backoff
- Add max retry limit (5 attempts)
```

---

## 4. 브랜치 규칙

### 브랜치 이름 패턴

| 유형 | 패턴 | 예시 |
|------|------|------|
| 기능 | `feature/<name>` | `feature/ws-reconnect` |
| 버그 수정 | `fix/<name>` | `fix/login-crash` |
| 릴리스 | `release/<version>` | `release/10.1.0` |

### 보호 브랜치

| 브랜치 | 직접 푸시 | 설명 |
|--------|----------|------|
| `main` | ❌ 금지 | PR 통해서만 병합 |
| `develop` | ❌ 금지 | PR 통해서만 병합 |

---

## 5. .gitignore 필수 항목

```gitignore
# Dependencies
node_modules/

# Build outputs
*.g.cs
*.g.ts
Generated/

# IDE
.idea/
.vs/
*.suo
*.user

# OS
.DS_Store
Thumbs.db

# Secrets
*.env
*.local
```

---

## Hard Conflicts (DoD)

아래 상태가 발견되면 **FAIL**:

1. `.sh` 파일에 CRLF 줄바꿈 사용
2. `.gitattributes` 파일 없음
3. `node_modules/` 디렉토리가 커밋됨
4. `*.env` 파일이 커밋됨

---

## Reference

- Policy SSOT: `skills/devian/03-ssot/SKILL.md`
- 동작 정본: `.gitattributes`, `.gitignore`
