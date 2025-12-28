# Devian – 69 C# UPM Scaffold

## Purpose

**Unity UPM 패키지 스캐폴딩 규칙과 템플릿을 정의한다.**

---

## Belongs To

**Build / Unity**

---

## 1. 개요

Devian.Tools는 `modules/upm/{Domain}/`을 Unity UPM 패키지 루트로 취급한다.

빌드 시 UPM 필수 파일이 **없으면 생성**, **있으면 절대 덮어쓰지 않는다**.

> ⚠️ `modules/cs/{Domain}/`은 C# 라이브러리 모듈(.csproj) 용도이며, UPM 패키지가 아니다.

---

## 2. moduleRoot 추론

`upmTargetDirs`에서 moduleRoot를 추론한다:

| upmTargetDir 패턴 | moduleRoot |
|------------------|------------|
| `.../Runtime/generated` | 2단계 상위 |
| 그 외 | 원본 그대로 |

예시:
- `modules/upm/Common/Runtime/generated` → `modules/upm/Common`
- `modules/upm/ws/Runtime/generated` → `modules/upm/ws`

---

## 3. 생성 대상 파일

| 파일 | 위치 | 설명 |
|------|------|------|
| `package.json` | moduleRoot | UPM 패키지 매니페스트 |
| `manifest.json` | moduleRoot | Unity 패키지 메타데이터 |
| `README.md` | moduleRoot | 패키지 설명 |
| `{AssemblyName}.asmdef` | moduleRoot/Runtime | Runtime 어셈블리 정의 |
| `{AssemblyName}.Editor.asmdef` | moduleRoot/Editor | Editor 어셈블리 정의 |

---

## 4. 덮어쓰기 금지 규칙 (MUST)

| 규칙 |
|------|
| 파일이 **이미 존재**하면 **절대 덮어쓰지 않는다** |
| 파일이 **없을 때만** 생성한다 |
| 기존 파일 내용을 **읽거나 수정하지 않는다** |

---

## 5. 템플릿

### package.json

```json
{
  "name": "com.devian.{domain}",
  "displayName": "Devian.{Domain}",
  "version": "0.1.0",
  "description": "Devian {domain} module - auto-generated contracts and protocols",
  "unity": "2021.3",
  "author": {
    "name": "Devian"
  },
  "dependencies": {},
  "keywords": [
    "devian",
    "{domain}"
  ]
}
```

### Runtime asmdef

```json
{
  "name": "Devian.{Domain}",
  "rootNamespace": "",
  "references": [
    // Common이 아닌 경우만: "Devian.Common"
  ],
  "includePlatforms": [],
  "excludePlatforms": [],
  "allowUnsafeCode": false,
  "overrideReferences": false,
  "precompiledReferences": [],
  "autoReferenced": true,
  "defineConstraints": [],
  "versionDefines": [],
  "noEngineReferences": true
}
```

### Editor asmdef

```json
{
  "name": "Devian.{Domain}.Editor",
  "rootNamespace": "",
  "references": [
    "Devian.{Domain}"
  ],
  "includePlatforms": [
    "Editor"
  ],
  "excludePlatforms": [],
  "allowUnsafeCode": false,
  "overrideReferences": false,
  "precompiledReferences": [],
  "autoReferenced": true,
  "defineConstraints": [],
  "versionDefines": [],
  "noEngineReferences": false
}
```

---

## 6. AssemblyName 규칙

| domain | AssemblyName | PackageName |
|--------|--------------|-------------|
| Common | Devian.Common | com.devian.common |
| ws | Devian.Ws | com.devian.ws |
| {domain} | Devian.{Domain} | com.devian.{domain} |

---

## 7. 생성 디렉토리 구조

```
modules/upm/{Domain}/
├── package.json
├── README.md
├── Runtime/
│   ├── Devian.{Domain}.asmdef
│   └── generated/
│       ├── *.g.cs            ← Devian.Tools 생성
│       └── ...
└── Editor/
    └── Devian.{Domain}.Editor.asmdef
```

---

## Hard Rules (MUST)

| # | 규칙 |
|---|------|
| 1 | 기존 파일 덮어쓰기 금지 |
| 2 | Runtime/generated 내 파일은 이 스킬 범위 아님 (다른 generator가 담당) |
| 3 | package.json의 dependencies는 빈 객체로 시작 |
| 4 | precompiledReferences는 빈 배열로 시작 |

---

## Soft Rules (SHOULD)

| # | 규칙 |
|---|------|
| 1 | noEngineReferences: Runtime은 true, Editor는 false |
| 2 | Common 외 도메인은 Devian.Common 참조 |

---

## Acceptance Criteria

| # | 조건 |
|---|------|
| 1 | 빌드 후 각 domain moduleRoot에 package.json 존재 |
| 2 | 빌드 후 각 domain moduleRoot/Runtime에 asmdef 존재 |
| 3 | 빌드 후 각 domain moduleRoot/Editor에 asmdef 존재 |
| 4 | 기존 파일이 있으면 덮어쓰지 않았음 |
| 5 | Runtime/generated/*.g.cs는 별도 generator가 생성 (이 스킬 범위 아님) |

---

## Related Skills

| Skill | 관계 |
|-------|------|
| `10-core-runtime` | modules/upm 구조 정의 |
| `60-build-pipeline` | 빌드 스펙 |
| `68-migration-checklist` | 검증 체크리스트 |

---

## Version History

| Version | Date | Changes |
|---------|------|---------|
| 1.0.0 | 2025-12-28 | Initial |
