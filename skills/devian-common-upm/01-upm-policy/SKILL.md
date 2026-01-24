# 02-upm-policy

Status: ACTIVE  
AppliesTo: v10  
Type: Policy / Entry Point

## Purpose

Devian UPM 패키지의 **정책 및 규약**을 정의하는 최상위 엔트리 문서이다.

---

## SSOT 원칙 (Hard Rule)

### 1. UPM 패키지 원본 경로

| 구분 | 경로 | 역할 |
|------|------|------|
| **수동 패키지** | `framework-cs/upm/{pkg}` | 수동 관리 패키지 원본 |
| **생성 패키지** | `framework-cs/upm/{pkg}` | 빌더가 생성하는 패키지 원본 |
| **최종 복사본** | `framework-cs/apps/UnityExample/Packages/{pkg}` | Unity 실행용 복사본 (직접 수정 금지) |

### 2. GeneratedRoot Clean+Copy 규칙 (Hard Rule)

**빌더는 GeneratedRoot(`Runtime/generated`, `Editor/Generated`)만 clean+copy 한다.**

- 수동 패키지의 수동 관리 파일(non-generated)은 빌더가 **절대 수정/삭제하지 않는다**
- 생성 패키지는 전체가 빌더 관리 대상

### 3. upm → Packages Sync 규칙 (Hard Rule)

```
upm/{pkg} → UnityExample/Packages/{pkg} (패키지 단위 clean+copy)
```

**Sync 후 검증:**
- `Packages/{pkg}`가 `upm/{pkg}`와 내용 일치
- `Packages/` 직접 수정 → 정책 위반, 다음 sync에서 덮어써짐

---

## 금지 경로 가드 (Hard Rule)

### com.devian.unity Editor/Generated 금지

**`com.devian.unity`에는 `Editor/Generated` 폴더가 생성되면 안 된다.**

- Editor/Generated for TableID inspection은 `com.devian.module.*` 패키지에 속함
- unity.common에 이 폴더가 존재하면 빌드 **즉시 FAIL**

빌더 가드 위치: `framework-ts/tools/builder/build.js` - `checkUnityCommonEditorGenerated()`

---

## 세부 문서

### Package Metadata
- `skills/devian-common-upm/03-package-metadata/SKILL.md`

### 패키지별 문서
- `skills/devian-common-upm/02-upm-bundles/SKILL.md`
- `skills/devian-common-upm/20-packages/com.devian.unity/SKILL.md`
- `skills/devian-common-upm/20-packages/com.devian.module.common/SKILL.md`

### Unity Components
- AssetManager: `skills/devian-common-upm/30-unity-components/10-asset-manager/SKILL.md`

---

## Related

- SSOT 정본: `skills/devian/03-ssot/SKILL.md`
- UPM Samples 정책: `skills/devian-common-upm-samples/01-upm-samples-policy/SKILL.md`
