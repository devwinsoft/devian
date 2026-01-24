# 18-build-sample

Status: ACTIVE  
AppliesTo: v10  
Type: Policy / Build Configuration

## SSOT

이 문서는 **Devian 샘플 빌드 설정 정책**을 정의한다.

---

## 개요

`input/input_sample.json`은 샘플 프로토콜/데이터를 빌드하기 위한 별도 설정 파일이다.

- 기존 `input_common.json`(프로덕션)과 분리
- 샘플 산출물은 **upm 패키지 Runtime 하위의 전용 폴더에만** 복사
- 프로덕션 빌드 경로/산출물을 오염시키지 않음

---

## 파일 위치

```
input/
├── input_common.json           ← 프로덕션 빌드 설정
├── input_sample.json    ← 샘플 빌드 설정
└── Protocols/
    ├── Game/            ← 프로덕션 프로토콜
    └── Sample/          ← 샘플 프로토콜
        ├── C2Sample.json
        └── Sample2C.json
```

---

## 빌드 실행

```bash
# 샘플 빌드
node framework-ts/tools/builder/build.js input/input_sample.json

# 프로덕션 빌드 (기존)
node framework-ts/tools/builder/build.js input/input_common.json
```

---

## Hard Rules (필수)

### 1. tempDir 분리 필수

**`input/input_sample.json`은 staging을 위해 반드시 `tempDir`를 명시해야 한다.**

- **권장값:** `"temp_sample"`
- **이유:** 빌더가 `tempDir`을 매 실행 **clean(rm -rf)** 하기 때문에, 프로덕션 staging(`temp`)과 분리하지 않으면 충돌한다.

**샘플 빌드는 `tempDir: "temp_sample"`을 강제한다. `temp` 공유 금지.**

### 2. csConfig/upmConfig 필수 (v10)

샘플 빌드도 v10 형식을 따르므로 `csConfig`와 `upmConfig`를 반드시 포함해야 한다:

```json
{
  "version": "10",
  "tempDir": "temp_sample",

  "csConfig": {
    "moduleDir": "../framework-cs/module",
    "generateDir": "../framework-cs/module"
  },

  "upmConfig": {
    "sourceDir": "../framework-cs/upm",
    // removed,
    "packageDir": "../framework-cs/apps/UnityExample/Packages"
  }
}
```

### 3. 샘플 빌드는 dataConfig.tableDirs를 비워두거나 샘플 전용 폴더만 사용

샘플 빌드의 데이터 출력 경로는 **반드시** 비어있거나 샘플 전용 하위 폴더만 지정해야 한다.

```json
"dataConfig": {
  "tableDirs": []
}
```

**금지 경로:**

```
# 프로덕션 경로 금지
../framework-cs/module/
../framework-cs/module/
../framework-ts/module/
../framework-ts/module/
../output/
```

### 4. 패키지 루트/Runtime 루트 clean+copy 금지

빌더가 `clean+copy` 정책을 사용하므로, Runtime 루트를 targetDir로 지정하면 기존 파일이 삭제된다.

**반드시 하위 폴더(`generated/`)만 지정할 것.**

### 5. 프로덕션 경로로 copy 금지

샘플 빌드가 프로덕션 모듈 경로로 산출물을 복사하면 안 된다:

- `../framework-cs/module/` ❌
- `../framework-cs/module/` ❌
- `../framework-ts/module/` ❌
- `../framework-ts/module/` ❌
- `../output/` ❌

---

## input_sample.json 예시

```json
{
  "version": "10",
  "tempDir": "temp_sample",

  "csConfig": {
    "moduleDir": "../framework-cs/module",
    "generateDir": "../framework-cs/module"
  },

  "tsConfig": {
    "moduleDir": "../framework-ts/module",
    "generateDir": "../framework-ts/module"
  },

  "upmConfig": {
    "sourceDir": "../framework-cs/upm",
    // removed,
    "packageDir": "../framework-cs/apps/UnityExample/Packages"
  },

  "dataConfig": {
    "tableDirs": []
  },

  "staticUpmPackages": [
    "com.devian.unity",
    "com.devian.unity"
  ],

  "domains": {},

  "protocols": [
    {
      "group": "Sample",
      "protocolDir": "./Protocols/Sample",
      "protocolFiles": ["C2Sample.json", "Sample2C.json"]
    }
  ]
}
```

### 주의사항

- Sample 프로토콜의 C# 출력은 `csConfig.generateDir` (module)로 반영됨
- Sample 프로토콜의 UPM 패키지는 `upmConfig.sourceDir` (upm)에 자동 생성됨
- 빌더가 `csConfig.generateDir` 하위에 `Devian.Protocol.{ProtocolName}/` 폴더를 자동 생성함
- Sample UPM 패키지는 `com.devian.protocol.sample`로 자동 명명됨

---

## 샘플 산출물 폴더 구조

```
# C# 모듈 (csConfig.generateDir)
framework-cs/module/
└── Devian.Protocol.Sample/
    ├── C2Sample.g.cs
    ├── Sample2C.g.cs
    └── Devian.Protocol.Sample.csproj

# UPM 패키지 (upmConfig.sourceDir → upmConfig.packageDir로 sync)
# Protocol UPM은 Runtime-only (Editor 생성 금지)
upm/com.devian.protocol.sample/
├── Runtime/
│   ├── Devian.Protocol.Sample.asmdef
│   ├── C2Sample.g.cs
│   └── Sample2C.g.cs
└── package.json

# Static UPM 패키지 (upm → packageDir로 sync)
upm/com.devian.unity/Runtime/Network/
├── Runtime/
│   └── Devian.Unity.Common.asmdef (Network 코드 포함)
├── Samples~/
│   └── BasicWsClient/
└── package.json
```

---

## asmdef 설정

### com.devian.protocol.sample/Runtime/Devian.Protocol.Sample.asmdef

Protocol UPM 패키지의 asmdef는 빌더가 자동 생성한다:

```json
{
  "name": "Devian.Protocol.Sample",
  "rootNamespace": "Devian.Protocol.Sample",
  "references": [
    "Devian.Core",
    "<어셈블리: Devian + .Module.Common>"
  ],
  "noEngineReferences": true
}
```

---

## 금지

- 새 UPM 패키지 생성/추가 금지 (기존 `com.devian.unity` 확장만)
- 새 "샘플 프로젝트/샘플 패키지" 도입 금지
- 기존 Common 입력/산출물 구조 변경 금지
- 샘플 빌드가 프로덕션 경로로 산출물 복사 금지

---

## Reference

- 빌더: `framework-ts/tools/builder/build.js`
- 프로덕션 빌드 설정: `input/input_common.json`
- 샘플 빌드 설정: `input/input_sample.json`
- Related: `skills/devian-common-upm-samples/01-upm-samples-policy/SKILL.md`
