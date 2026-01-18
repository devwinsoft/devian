# 18-build-sample

Status: ACTIVE  
AppliesTo: v10  
Type: Policy / Build Configuration

## SSOT

이 문서는 **Devian 샘플 빌드 설정 정책**을 정의한다.

---

## 개요

`input/build_sample.json`은 샘플 프로토콜/데이터를 빌드하기 위한 별도 설정 파일이다.

- 기존 `build.json`(프로덕션)과 분리
- 샘플 산출물은 **기존 패키지 Runtime 하위의 전용 폴더에만** 복사
- 프로덕션 빌드 경로/산출물을 오염시키지 않음

---

## 파일 위치

```
input/
├── build.json           ← 프로덕션 빌드 설정
├── build_sample.json    ← 샘플 빌드 설정
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
node framework-ts/tools/builder/build.js input/build_sample.json

# 프로덕션 빌드 (기존)
node framework-ts/tools/builder/build.js input/build.json
```

---

## Hard Rules (필수)

### 1. tempDir 분리 필수

**`input/build_sample.json`은 staging을 위해 반드시 `tempDir`를 명시해야 한다.**

- **권장값:** `"temp_sample"`
- **이유:** 빌더가 `tempDir`을 매 실행 **clean(rm -rf)** 하기 때문에, 프로덕션 staging(`temp`)과 분리하지 않으면 충돌한다.

**샘플 빌드는 `tempDir: "temp_sample"`을 강제한다. `temp` 공유 금지.**

```json
{
  "version": "10",
  "tempDir": "temp_sample",  // ← 필수! "temp" 사용 금지
  ...
}
```

### 2. targetDirs는 샘플 전용 하위 폴더만 사용

샘플 빌드의 `csTargetDir`, `dataTargetDirs`은 **반드시** 기존 패키지의 Runtime 하위 전용 폴더를 가리켜야 한다.

**허용 경로:**

```
.../com.devian.unity.network/Runtime/Generated.Sample/
.../com.devian.unity.network/Runtime/SampleData/
```

**금지 경로:**

```
# Runtime 루트 금지 (clean 시 런타임 파일 삭제됨)
.../com.devian.unity.network/Runtime/

# 프로덕션 경로 금지
../framework-cs/modules/
../framework-ts/modules/
```

### 3. 패키지 루트/Runtime 루트 clean+copy 금지

빌더가 `clean+copy` 정책을 사용하므로, Runtime 루트를 targetDir로 지정하면 기존 파일이 삭제된다.

**반드시 하위 폴더(`Generated.Sample/`, `SampleData/`)만 지정할 것.**

### 4. 프로덕션 targetDirs로 copy 금지

샘플 빌드가 프로덕션 모듈 경로로 산출물을 복사하면 안 된다:

- `../framework-cs/modules/` ❌
- `../framework-ts/modules/` ❌
- `../output/` ❌

---

## build_sample.json 예시

```json
{
  "version": "10",
  "tempDir": "temp_sample",

  "protocols": [
    {
      "group": "Sample",
      "protocolDir": "./Protocols/Sample",
      "protocolFiles": ["C2Sample.json", "Sample2C.json"],
      "csTargetDir": "../framework-cs/apps/UnityExample/Packages/com.devian.unity.network/Runtime/Generated.Sample"
    }
  ]
}
```

### 주의사항

- 빌더가 `csTargetDir` 하위에 `Devian.Network.{group}/` 폴더를 자동 생성함
- 위 예시의 실제 산출 경로: `.../Runtime/Generated.Sample/Devian.Network.Sample/`

---

## 샘플 산출물 폴더 구조

```
com.devian.unity.network/
├── Runtime/
│   ├── Generated.Sample/                     ← 샘플 전용
│   │   ├── Devian.Unity.Network.Sample.asmdef
│   │   └── Devian.Network.Sample/
│   │       ├── C2Sample.g.cs
│   │       ├── Sample2C.g.cs
│   │       └── Devian.Network.Sample.csproj
│   └── SampleData/                           ← 샘플 데이터 (필요 시)
│       └── json/
├── Samples~/
│   └── BasicWsClient/
└── package.json
```

---

## asmdef 설정

### Runtime/Generated.Sample/Devian.Unity.Network.Sample.asmdef

```json
{
  "name": "Devian.Unity.Network.Sample",
  "rootNamespace": "Devian.Network.Sample",
  "references": [
    "Devian.Core",
    "Devian.Network",
    "Devian.Protobuf"
  ],
  "noEngineReferences": true
}
```

---

## 금지

- 새 UPM 패키지 생성/추가 금지 (기존 `com.devian.unity.network` 확장만)
- 새 "샘플 프로젝트/샘플 패키지" 도입 금지
- 기존 Common 입력/산출물 구조 변경 금지
- 샘플 빌드가 프로덕션 경로로 산출물 복사 금지

---

## Reference

- 빌더: `framework-ts/tools/builder/build.js`
- 프로덕션 빌드 설정: `input/build.json`
- 샘플 빌드 설정: `input/build_sample.json`
- Related: `skills/devian/16-unity-upm-samples/SKILL.md`
