# 02-install — Build Automation Prerequisites Installation

Status: ACTIVE
AppliesTo: v11

빌드 자동화 파이프라인에 필요한 외부 도구 설치 절차를 정의한다.
Build/Symbol Upload별 필수/선택 도구를 구분하고, macOS 기준 설치 명령을 제공한다.

---

## 도구 매트릭스

| 도구 | 사용 시점 | 필수 여부 | 용도 |
|------|-------|-----------|------|
| Firebase SDK (Unity) | Build, Symbol Upload | 필수 | Crashlytics 크래시 리포팅 |
| Firebase CLI | Symbol Upload | 필수 | 심볼 업로드 (`crashlytics:symbols:upload`) |
| Node.js | Symbol Upload | 필수 | Firebase CLI 런타임 |
| Xcode CLI Tools | Build (iOS) | 필수 | iOS 빌드/아카이브 |

---

## 1. Firebase SDK (Unity)

Firebase Unity SDK는 `.unitypackage`로 설치한다.

### 설치

1. [Firebase Unity SDK 다운로드](https://firebase.google.com/download/unity)
2. Unity에서 `Assets > Import Package > Custom Package`
3. 최소 패키지:
   - `FirebaseCrashlytics.unitypackage` (필수)
   - `FirebaseAnalytics.unitypackage` (권장 — breadcrumb 로그용)

### 플랫폼 설정 파일

| 플랫폼 | 파일 | 위치 |
|--------|------|------|
| Android | `google-services.json` | `Assets/StreamingAssets/` |
| iOS | `GoogleService-Info.plist` | `Assets/StreamingAssets/` |

Firebase Console > 프로젝트 설정 > 앱 추가에서 다운로드한다.

### 확인

Settings 탭 Prerequisites에서 다음이 ✅로 표시되어야 한다:
- Firebase SDK
- google-services.json (Android)
- GoogleService-Info.plist (iOS)

---

## 2. Firebase CLI

Firebase CLI는 Symbol Upload에 필수이다.
`#!/usr/bin/env node`로 실행되므로 Node.js가 먼저 설치되어 있어야 한다.

### 설치 (npm)

```bash
# Node.js가 이미 설치된 경우
npm install -g firebase-tools
```

### 설치 (standalone — Node.js 불필요)

```bash
# macOS/Linux standalone binary
curl -sL https://firebase.tools | bash
```

### 로그인

```bash
firebase login
```

### 확인

```bash
firebase --version    # 버전 출력 확인
firebase projects:list  # 프로젝트 목록 확인
```

### Unity Editor에서의 PATH 문제

Unity Editor는 GUI 앱이므로 쉘 PATH를 상속받지 못한다.
`BuildAutomationUtil.CreateStartInfo()`에서 다음 경로를 자동 탐색하여 PATH에 주입한다:

- `/usr/local/bin`
- `/opt/homebrew/bin`
- `~/.nvm/versions/node/{latest}/bin`
- `~/.npm-global/bin`
- Firebase CLI 실행 파일의 부모 디렉토리

자동 탐색이 실패하면 Settings > CLI Paths > Firebase CLI Path에 절대경로를 수동 지정한다.
터미널에서 `which firebase`로 경로를 확인할 수 있다.

---

## 설치 순서 (권장)

```
1. Xcode CLI Tools  →  xcode-select --install
2. Node.js           →  brew install node  (또는 nvm)
3. Firebase CLI      →  npm install -g firebase-tools
4. Firebase login    →  firebase login
5. Firebase SDK      →  Unity에서 unitypackage import
6. 플랫폼 설정 파일   →  google-services.json / GoogleService-Info.plist
```

---

## Related

- [00-overview](../00-overview/SKILL.md) — 그룹 개요
- [10-settings](../10-settings/SKILL.md) — Settings (Prerequisites 체크)
- [30-symbol-upload](../30-symbol-upload/SKILL.md) — Firebase CLI 사용처
