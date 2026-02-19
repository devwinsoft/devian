# 30-editor-complex-drawer


Status: ACTIVE
AppliesTo: v10
SSOT: skills/devian/10-module/03-ssot/SKILL.md


## Overview
Unity Inspector에서 `Devian.CInt`, `Devian.CFloat`, `Devian.CString`를 평문 값처럼 편집할 수 있게 하는 PropertyDrawer를 제공한다.


## Hard Rules (MUST)
1. UnityEditor 전용 코드이며 Runtime 어셈블리에 포함되면 안 된다.
2. `Debug.*` 사용 금지. 필요한 로그는 `Devian.Log` 사용.
3. 마스킹 저장 구조(save1/save2)는 런타임 타입 규격을 변경하지 않는다.


## Directory Structure
- framework-cs/upm/com.devian.foundation/Editor/Complex/CIntPropertyDrawer.cs
- framework-cs/upm/com.devian.foundation/Editor/Complex/CFloatPropertyDrawer.cs
- framework-cs/upm/com.devian.foundation/Editor/Complex/CStringPropertyDrawer.cs
