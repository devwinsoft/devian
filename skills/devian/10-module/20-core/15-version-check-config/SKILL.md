# 15-version-check-config

Status: ACTIVE
AppliesTo: v11
SSOT: skills/devian/10-module/03-ssot/SKILL.md

## Purpose

버전 JSON 파일의 공통 데이터 모델.
Runtime(RemoteDataManager 버전 체크)과 Editor(BuildAutomationWindow 버전 게시) 양쪽에서 사용한다.

---

## Data Structure

```csharp
[Serializable]
public sealed class VersionCheckConfig
{
    public string currentVersion = string.Empty;
    public string minVersion = string.Empty;
    public string update_url = string.Empty;
}
```

- `[Serializable]`: `JsonUtility.FromJson<VersionCheckConfig>()` 호환
- `sealed class`: 상속 불필요, 참조 타입
- `currentVersion`: 권장 최신 버전 (이 버전 미만이면 업데이트 권고)
- `minVersion`: 최소 필수 버전 (이 버전 미만이면 강제 업데이트)
- `update_url`: 스토어/업데이트 URL

---

## JSON Format

버전 JSON 파일 (`release/version_aos.json`, `release/version_ios.json`):

```json
{
    "currentVersion": "1.2.3",
    "minVersion": "1.0.0",
    "update_url": "https://play.google.com/store/apps/details?id=..."
}
```

---

## 사용처

| 위치 | 용도 | 참조 방식 |
|------|------|-----------|
| `RemoteDataManager` (Runtime) | 서버에서 버전 JSON fetch → 클라이언트 버전과 비교 | `JsonUtility.FromJson<VersionCheckConfig>()` |
| `BuildAutomationWindow` (Editor) | 버전 JSON 읽기/쓰기, Publish UI 표시 | `ReadVersionFromJson()`, `UpdateVersionJson()` |

### Runtime 흐름 (RemoteDataManager)

```
fetchVersionCheckConfigAsync() → VersionCheckConfig 파싱
  → evaluateVersionCheck(clientVersion, config)
    → clientVersion < minVersion → ForceUpdate
    → clientVersion < currentVersion → RecommendUpdate
    → else → Success
```

### Editor 흐름 (BuildAutomationWindow)

```
RefreshLastVersions() → ReadVersionFromJson() → VersionCheckConfig (last)
  → 편집 필드용 별도 인스턴스 생성 (edit)
  → RunVersionPublish() → UpdateVersionJson(path, editInfo)
    → 변경 있으면 git commit
```

---

## Files (SSOT)

```
framework-cs/module/Devian/src/Core/VersionCheckConfig.cs                              # C# module 정본
framework-cs/upm/com.devian.foundation/Runtime/Module/Core/VersionCheckConfig.cs       # UPM 미러
framework-cs/apps/UnityExample/Packages/com.devian.foundation/Runtime/Module/Core/     # Packages sync
```

- asmdef: `Devian.Core` (`com.devian.foundation/Runtime/Module/Devian.Core.asmdef`)
- Samples asmdef (`Devian.Samples.MobilePackage`)에서 `Devian.Core`를 참조하므로 Runtime에서 접근 가능

---

## Reference

- Parent: `skills/devian/10-module/20-core/00-overview/SKILL.md`
- Policy: `skills/devian/10-module/01-policy/SKILL.md`
- Related: [14-version-number](../14-version-number/SKILL.md) — VersionNumber 구조체 (비교 연산)
- Related: [29-remote-data-system](../../../devian-unity/50-mobile-package/29-remote-data-system/10-remote-data-manager/SKILL.md) — RemoteDataManager
- Related: [40-version-publish](../../../devian-unity/50-mobile-package/70-build-operation/40-version-publish/SKILL.md) — Version Publish
