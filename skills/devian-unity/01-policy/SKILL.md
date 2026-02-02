# devian-unity — Policy

Status: ACTIVE  
AppliesTo: v10  
Type: Policy / Entry Point

## Purpose

Devian Unity 패키지의 **정책 및 규약**을 정의하는 최상위 엔트리 문서이다.

---

## SSOT 원칙 (Hard Rule)

### 1. UPM 패키지 원본 경로

| 구분 | 경로 | 역할 |
|------|------|------|
| **수동 패키지** | `framework-cs/upm/{pkg}` | 수동 관리 패키지 원본 |
| **생성 패키지** | `framework-cs/upm/{pkg}` | 빌더가 생성하는 패키지 원본 |
| **최종 복사본** | `framework-cs/apps/UnityExample/Packages/{pkg}` | Unity 실행용 복사본 (직접 수정 금지) |

### 2. GeneratedRoot Clean+Copy 규칙 (Hard Rule)

**빌더는 GeneratedRoot(`Runtime/Generated`, `Editor/Generated`)만 clean+copy 한다.**

- 수동 패키지의 수동 관리 파일(non-generated)은 빌더가 **절대 수정/삭제하지 않는다**
- 생성 패키지는 전체가 빌더 관리 대상

### 3. upm → Packages Sync 규칙 (Hard Rule)

```
upm/{pkg} → UnityExample/Packages/{pkg} (패키지 단위 clean+copy)
```

**Sync 후 검증:**
- `Packages/{pkg}`가 `upm/{pkg}`와 내용 일치
- `Packages/` 직접 수정 → 정책 위반, 다음 sync에서 덮어써짐

### 4. .meta 파일 SSOT (Hard Rule)

**UPM 소스(`upm/{pkg}`)에 .meta 파일 포함 필수**

| 항목 | 규칙 |
|------|------|
| **정본 위치** | `framework-cs/upm/{pkg}/*.meta` |
| **Unity 복사본** | `framework-cs/apps/UnityExample/Packages/{pkg}/*.meta` |
| **수정 금지** | Unity Packages 폴더에서 직접 .meta 수정 금지 |
| **역방향 동기화** | Packages → UPM 복사 필요시 `npm run sync-meta` 사용 |

**빌드 검증:**
- `syncUpmToPackageDir()` 실행 후 `checkUpmPackagesSynced()` 자동 호출
- `assertDirTreeEqual()`: 디렉토리 트리 완전 일치 검증 (파일 + 내용)
- 불일치 시 빌드 **즉시 FAIL**

**도구:**
- `npm -w builder run sync-meta -- <config>` - Packages의 .meta를 UPM으로 역복사 (일회성 마이그레이션용)
- 위치: `framework-ts/tools/scripts/sync-meta.js`

### 5. 대규모 리네임 작업 규칙 (Hard Rule)

**C# 파일 대규모 리네임 시 다음 절차를 따른다:**

#### 금지 사항
- `.meta` 파일 내용 직접 편집 금지
- 파일별 개별 Read → Edit → Write 순차 처리 금지
- UPM과 UnityExample을 개별적으로 수정 금지

#### 필수 절차

```bash
# 1. 파일 리네임 (git mv 사용 - .meta 자동 처리, GUID 유지)
git mv OldName.cs NewName.cs

# 2. 내용 일괄 치환 (sed 사용)
find . -name "*.cs" -exec sed -i 's/OldName/NewName/g' {} +

# 3. UPM → UnityExample 폴더 단위 복사
cp -r upm/{pkg}/Runtime/Unity/TargetDir/* UnityExample/Packages/{pkg}/Runtime/Unity/TargetDir/

# 4. UnityExample의 구 파일 정리 (필요시)
rm -f UnityExample/Packages/{pkg}/.../OldName.cs
rm -f UnityExample/Packages/{pkg}/.../OldName.cs.meta
```

#### 핵심 원칙
| 작업 | 도구 | 이유 |
|------|------|------|
| 파일 리네임 | `git mv` | Unity GUID 자동 유지 |
| 내용 치환 | `sed` / `find -exec` | 일괄 처리로 속도 향상 |
| 패키지 동기화 | `cp -r` (폴더 단위) | 파일별 처리 불필요 |

---

## 금지 경로 가드 (Hard Rule)

### com.devian.foundation Editor/Generated 금지

**`com.devian.foundation`에는 `Editor/Generated` 폴더가 생성되면 안 된다.**

- Editor/Generated for TableID inspection은 `com.devian.domain.*` 패키지에 속함
- foundation에 이 폴더가 존재하면 빌드 **즉시 FAIL**

빌더 가드 위치: `framework-ts/tools/builder/build.js` - `checkUnityCommonEditorGenerated()`

---

## 세부 문서

### Package Metadata
- `skills/devian-unity/04-package-metadata/SKILL.md`

### 패키지별 문서
- `skills/devian-unity/02-unity-bundles/SKILL.md`
- `skills/devian-core/03-ssot/SKILL.md` (Foundation Package SSOT)
- `skills/devian-unity/20-packages/com.devian.domain.common/SKILL.md`

### Unity Components
- AssetManager: `skills/devian-unity/30-unity-components/10-asset-manager/SKILL.md`

---

## Related

- SSOT 정본: `skills/devian-core/03-ssot/SKILL.md`
- UPM Samples 정책: `skills/devian-unity-samples/02-samples-authoring-guide/SKILL.md`
