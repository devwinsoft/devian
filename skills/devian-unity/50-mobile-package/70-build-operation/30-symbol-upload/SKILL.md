# 30-symbol-upload — Symbol Upload

Status: ACTIVE
AppliesTo: v11

Firebase Crashlytics에 심볼 파일을 업로드한다.
네이티브 크래시 스택트레이스를 함수명으로 해석하기 위해 필요하다.

---

## 심볼 파일 경로

심볼 파일 경로는 **EditorWindow(Release 탭)에서 절대 경로로 관리**한다:

| 플랫폼 | 파일 | 기본 탐색 경로 |
|--------|------|----------------|
| Android | symbols.zip | `{buildOutputDir}/Android/*.symbols.zip` |
| iOS | dSYMs | `{buildOutputDir}/iOS/app.xcarchive/dSYMs/` |

### 경로 입력 방식

1. **자동 입력**: Build 성공 시 `AutoFillSymbolPaths()`가 빌드 산출물 디렉토리를 탐색하여 자동 채움
2. **수동 선택**: Release 탭 Symbol Upload 섹션에서 `[...]` 브라우저 버튼으로 직접 선택
3. **직접 입력**: 텍스트 필드에 절대 경로를 직접 타이핑

경로 필드는 EditorWindow의 런타임 상태(Settings에 저장하지 않음). 빌드별로 달라지는 값이므로.

---

## 업로드 실행

### SymbolUploader API

Firebase CLI를 호출하여 심볼을 업로드한다. **호출자가 절대 경로를 명시적으로 전달**한다:

```csharp
// Android
Task<bool> SymbolUploader.UploadAndroid(
    BuildAutomationSettings settings,
    string symbolsZipPath,      // 절대 경로
    CancellationToken ct)

// iOS
Task<bool> SymbolUploader.UploadIOS(
    BuildAutomationSettings settings,
    string dsymPath,            // 절대 경로 (dSYMs 디렉토리)
    CancellationToken ct)
```

내부 동작:
1. App ID 검증 → 파일/디렉토리 존재 확인
2. 파일 크기 로그
3. `firebase crashlytics:symbols:upload --app={appId} "{path}"` 실행
4. CancellationToken + 5분 타임아웃

---

## 검증

업로드 후 확인 항목:

| 확인 | 방법 |
|------|------|
| CLI 성공 | exit code 0 |
| Firebase Console | Crashlytics > 해당 앱 > "Missing dSYMs" 경고 없음 |
| 스택트레이스 해석 | 테스트 크래시 발생 후 함수명 표시 확인 |

테스트 크래시 (개발 빌드 전용):

```csharp
Firebase.Crashlytics.Crashlytics.LogException(
    new System.Exception("Symbol upload verification test"));
```

---

## 완료 조건

- [ ] 활성화된 플랫폼의 심볼 업로드 성공 (exit code 0)
- [ ] Firebase Console에서 Missing dSYM 경고 없음

---

## Related

- [20-build](../20-build/SKILL.md) — Build (산출물 제공)
- [10-settings](../10-settings/SKILL.md) — Firebase App ID 설정
