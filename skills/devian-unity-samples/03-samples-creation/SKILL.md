# devian-unity-samples — Samples~ Creation




## Scope
- `Samples~`(UPM Samples) 샘플을 **새로 추가**할 때 필요한 "생성 절차(체크리스트)"만 다룬다.
- 정책/규약의 근거는 `02-samples-authoring-guide`를 SSOT로 한다.




## Prerequisites
- Samples 작성 정책/규약: `skills/devian-unity-samples/02-samples-authoring-guide/SKILL.md`




## Source of Truth (Hard)
- 샘플 원본은 반드시 아래 위치에서만 작성한다:
  - `framework-cs/upm/<packageName>/Samples~/<SampleName>/...`
- 아래는 "빌드 출력물(복사본)"이므로 직접 수정 금지:
  - `framework-cs/apps/UnityExample/Packages/<packageName>/...`
- Import된 샘플(Assets 아래)은 사용자 수정본이므로 Devian이 직접 수정하지 않는다:
  - `framework-cs/apps/UnityExample/Assets/Samples/...`




## Checklist — 샘플 생성 절차




### 1) 샘플 폴더 생성
- 위치:
  - `framework-cs/upm/<packageName>/Samples~/<SampleName>/`
- 최소 구성:
  - `README.md`
  - `Runtime/`
  - (필요 시) `Editor/`




### 2) asmdef 생성 (Hard)
- Runtime asmdef는 필수:
  - 예: `Runtime/Devian.Samples.<SampleName>.asmdef`
- Editor 코드를 쓸 경우:
  - `Editor/` 폴더 분리 + Editor asmdef 생성
  - Editor asmdef는 `includePlatforms: ["Editor"]` 필수
- Runtime 코드에서 `using UnityEditor;` 금지




### 3) UPM Samples UI 등록 (package.json)
- 패키지의 `package.json`에 `samples[]` 항목을 추가한다.
- 예시(형식):
```json
{
  "samples": [
    {
      "displayName": "<DisplayName>",
      "description": "<Description>",
      "path": "Samples~/<SampleName>"
    }
  ]
}
```




### 4) 미러(동기화) 확인 (Hard)
- `framework-cs/apps/UnityExample/Packages/<packageName>/Samples~/<SampleName>/...`에 동일 샘플이 반영되는지 확인한다.
- 직접 수정 금지: 반영이 안 되면 "동기화/빌드" 문제로 처리한다(문서에서만 안내).




## DoD (Definition of Done)


**Hard (반드시 0)**
- 샘플 원본이 `framework-cs/upm/**/Samples~/**`에 존재
- `package.json.samples[]`에 등록되어 UPM Samples UI에서 노출
- UnityExample `Packages/**` 미러가 생성/동기화되어 일치
- Runtime/Editor 경계 위반 없음(Runtime에서 UnityEditor 사용 금지)


**Soft (선택)**
- README에 "무엇을 제공/무엇을 제공하지 않는지"가 한눈에 보이게 정리
