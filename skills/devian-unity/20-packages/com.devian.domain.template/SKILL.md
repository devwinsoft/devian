# com.devian.domain.{DomainName} — UPM 패키지 공통 템플릿

Status: ACTIVE  
AppliesTo: v10

## SSOT

이 문서는 `com.devian.domain.{DomainName}` UPM 패키지의 **공통 규약(템플릿)**을 정의한다.

각 도메인별 패키지(`com.devian.domain.common`, `com.devian.domain.game` 등)는 이 템플릿을 기반으로 하며, 도메인별 예외사항은 각 패키지 스킬에서 추가로 정의한다.

---

## 목표

- Devian 도메인 모듈을 Unity UPM 패키지로 제공한다.
- 도메인 데이터(Table Entity, Container)와 선택적 Features를 포함한다.
- keyed table이 있는 경우 Unity Editor용 TableID Inspector 바인딩을 생성한다.
- UnityEngine 직접 의존 없이 순수 C# 코드만 포함한다(Editor 코드 제외).

---

## 의존 방향 정책 (핵심)

```
com.devian.foundation (base - Core + Unity 통합)
       ↑
com.devian.domain.{DomainName} (이 템플릿 - foundation 의존)
```

> **Hard Rule:** `com.devian.foundation` → `com.devian.domain.*` 의존 **금지** (순환 방지)

---

## 패키지 루트 (SSOT)

```
framework-cs/upm/com.devian.domain.{domainname}/
```

> **Note**: `framework-cs/apps/UnityExample/Packages/com.devian.domain.{domainname}`는 sync 대상(배포/테스트)일 뿐, SSOT가 아니다.

---

## 패키지 레이아웃 (공통)

```
com.devian.domain.{domainname}/
├── package.json
├── Runtime/
│   ├── Devian.Domain.{DomainName}.asmdef
│   ├── Generated/
│   │   └── {DomainName}.g.cs              (generated - TableGen)
│   └── Features/                           (선택 - 도메인별 features)
│       └── ...
└── Editor/
    ├── Devian.Domain.{DomainName}.Editor.asmdef
    └── Generated/
        └── {TableName}_ID.Editor.cs        (keyed table 있을 때 생성)
```

---

## TableID Editor 바인딩 규칙 (Hard Rule)

**keyed table(primaryKey 있는 테이블)이 하나라도 있으면 TableID Inspector 바인딩이 생성된다.**

### 생성 조건

- **조건**: 도메인에 `primaryKey`가 정의된 테이블이 1개 이상 존재
- **생성 주체**: `build.js` (`generateDomainUpmScaffold`)

### 생성 위치

```
com.devian.domain.{domainname}/Editor/Generated/{TableName}_ID.Editor.cs
```

### 파일명 규칙

| 규칙 | 예시 |
|------|------|
| `{TableName}_ID.Editor.cs` | `TestSheet_ID.Editor.cs` |

### 클래스명 규칙

| 클래스 | 규칙 | 예시 |
|--------|------|------|
| Selector | `{DomainName}_{TableName}_ID_Selector` | `Game_TestSheet_ID_Selector` |
| Drawer | `{DomainName}_{TableName}_ID_Drawer` | `Game_TestSheet_ID_Drawer` |

### 네임스페이스 규칙

**모든 코드는 단일 네임스페이스 `Devian`을 사용한다.**

```csharp
namespace Devian
{
    public class Game_TestSheet_ID_Selector : EditorID_SelectorBase { ... }
    public class Game_TestSheet_ID_Drawer : EditorID_DrawerBase<Game_TestSheet_ID_Selector> { ... }
}
```

> **주의**: `Devian.Domain.{DomainName}` 같은 서브네임스페이스를 사용하지 않는다.

### 베이스 클래스 참조

베이스 클래스는 `com.devian.foundation` 패키지에서 제공된다:

| 파일 | 경로 |
|------|------|
| `EditorID_DrawerBase.cs` | `com.devian.foundation/Editor/TableId/EditorID_DrawerBase.cs` |
| `EditorID_SelectorBase.cs` | `com.devian.foundation/Editor/TableId/EditorID_SelectorBase.cs` |
| `EditorRectUtil.cs` | `com.devian.foundation/Editor/TableId/EditorRectUtil.cs` |

- Selector UX(Hard): 항목 클릭 즉시 적용(Apply 버튼 없음). (EditorID_SelectorBase 규약)
- Group 지원(Hard): 테이블에 `group:true` 컬럼이 있으면 Selector는 groupKey 목록을 표시하고, 선택 시 대표 PK(min PK)를 Value에 저장한다. Inspector 표시도 groupKey로 한다. (EditorID_SelectorBase 규약)

> **상세 API**: `skills/devian/03-ssot/SKILL.md` (Foundation Package SSOT) 참조

---

## asmdef 의존성 정책 (Hard Rule)

### Runtime asmdef

```json
{
  "name": "Devian.Domain.{DomainName}",
  "rootNamespace": "Devian.Domain.{DomainName}",
  "references": [
    "Devian.Core"
  ]
}
```

> **Note**: 도메인별로 추가 참조가 필요할 수 있음 (예: Common은 `Newtonsoft.Json` 추가)

### Editor asmdef (TableID 생성 시)

**Editor asmdef는 반드시 `Devian.Unity`, `Devian.Unity.Editor`를 참조해야 한다.**

```json
{
  "name": "Devian.Domain.{DomainName}.Editor",
  "rootNamespace": "Devian.Domain.{DomainName}",
  "references": [
    "Devian.Domain.{DomainName}",
    "Devian.Unity",
    "Devian.Unity.Editor"
  ],
  "includePlatforms": ["Editor"]
}
```

> **빌더 자동 패치**: `build.js`가 keyed table 존재 시 Editor asmdef의 `references`에 Unity 참조를 자동으로 추가한다.

---

## package.json 정책

| 필드 | 규칙 |
|------|------|
| name | `com.devian.domain.{domainname}` (소문자) |
| version | `0.1.0` (다른 com.devian.* 패키지와 동일) |
| displayName | `Devain Domain {DomainName}` |
| unity | `2021.3` |
| dependencies | `com.devian.foundation: 0.1.0` (최소) |

---

## 빌더 생성 정책 (Hard Rule)

**이 UPM 패키지는 빌더(`build.js`)가 staging에 생성 후 clean+copy로 targetDir에 덮어쓴다.**

- staging에 포함되지 않은 파일은 clean+copy 이후 삭제된다.
- Features가 있는 경우 **빌더가 staging에 복사**해야 한다.
- `upmConfig.packageDir`가 UnityExample/Packages를 가리키면 해당 디렉토리는 **generated output**으로 취급된다.

---

## DoD (완료 정의) — Hard Gate

keyed table이 있는 도메인의 경우:

- [ ] `Editor/Generated/{TableName}_ID.Editor.cs` 파일 존재
- [ ] Editor asmdef에 `Devian.Unity`, `Devian.Unity.Editor` 참조 포함
- [ ] 생성된 클래스가 `EditorID_DrawerBase`, `EditorID_SelectorBase`를 상속
- [ ] 네임스페이스가 `Devian`으로 통일됨

keyed table이 없는 도메인의 경우:

- [ ] `Editor/Generated/` 폴더가 비어있거나 존재하지 않음

---

## 금지

- **UnityEngine 의존 코드 포함 금지**: 이 패키지의 Runtime 코드는 "공용 코어"로 유지한다.
- Runtime 코드에서 `UnityEngine.*` namespace 직접 참조 금지.
- **서브네임스페이스 사용 금지**: Editor 생성물도 `namespace Devian` 단일 사용.
- **clean+copy 정책 무시 금지**: targetDir에 수동으로만 파일을 두면 재빌드 시 삭제됨.
- **TableID 베이스 클래스 중복 정의 금지**: `com.devian.foundation`이 제공하는 베이스만 사용.

---

## Reference

- Related: `skills/devian/03-ssot/SKILL.md` (Foundation Package SSOT, TableID 베이스 클래스)
- Related: `skills/devian-unity/02-unity-bundles/SKILL.md`
- Related: `skills/devian-unity/03-package-metadata/SKILL.md`
- Related: `skills/devian/03-ssot/SKILL.md`
