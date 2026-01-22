# Devian v10 — Generated Integration Rules

Status: ACTIVE  
AppliesTo: v10  
SSOT: skills/devian/03-ssot/SKILL.md

## Purpose

generated 산출물을 프로젝트에 통합할 때의 **소유권/폴더/수정 금지 규칙**을 정의한다.

이 문서는 "generated를 어떻게 취급할지"만 말한다.
실제 생성 파일 목록은 **런타임/제너레이터 코드**를 정답으로 본다.

---

## Ownership

- `framework-cs/module-gen/**/*.g.cs`, `framework-ts/module-gen/**/*.g.ts` 는 **기계 소유**다.
- 사람은 이 파일을 수정하지 않는다.
- 수정이 필요하면 입력(contracts/tables/protocols) 또는 generator 코드 변경으로 해결한다.

## Commit Policy

- generated는 **커밋 대상**이다.
  - 빌드 없이도 소비자가 타입/codec을 사용할 수 있어야 한다.

## Directory Expectations

정확한 출력 루트는 input_common.json의 csConfig/tsConfig가 정본이다.

### 출력 경로 규칙

| 타겟 | Domain 출력 경로 | Protocol 출력 경로 |
|------|------------------|-------------------|
| C# | `{csConfig.generateDir}/Devian.Module.{Domain}/` | `{csConfig.generateDir}/Devian.Network.{Group}/` |
| TS | `{tsConfig.generateDir}/devian-module-{domain}/` | `{tsConfig.generateDir}/devian-network-{group}/` |
| Data (ndjson) | `{dataConfig.targetDirs}/{Domain}/ndjson/` | - |
| Data (bin) | `{dataConfig.targetDirs}/{Domain}/pb64/` (pk 옵션 테이블만) | - |

### 권장 구조

```
framework-cs/
├── module/                                     # 수동 관리 (Core 런타임 등)
│   ├── Devian.Core/
│   ├── Devian.Protobuf/
│   └── Devian.Network/
├── module-gen/                                 # 생성 산출물 (기계 소유)
│   ├── Devian.Module.{Domain}/
│   │   └── generated/
│   │       └── {Domain}.g.cs
│   └── Devian.Network.{Group}/
│       ├── Devian.Network.{Group}.csproj
│       └── {ProtocolName}.g.cs

framework-ts/
├── module/                                     # 수동 관리 (Core 런타임 등)
│   ├── devian-core/
│   ├── devian-protobuf/
│   └── devian-network/
├── module-gen/                                 # 생성 산출물 (기계 소유)
│   ├── devian-module-{domain}/
│   │   ├── generated/
│   │   │   └── {Domain}.g.ts
│   │   └── index.ts
│   └── devian-network-{group}/
│       ├── {ProtocolName}.g.ts
│       └── index.ts

output/
└── {Domain}/
    ├── ndjson/
    │   └── *.json
    └── pb64/
        └── *.asset  # pk 옵션 테이블만
```

> 실제 폴더명/레이아웃은 프로젝트 구성에 따라 달라질 수 있으며, 코드가 정답이다.

---

## TypeScript Module Configuration

generated TS 코드가 `@devian/core`, `@devian/protobuf` 모듈을 import하기 위해 **paths alias 설정**이 필요하다.

### framework/ts/tsconfig.json (루트)

```json
{
  "compilerOptions": {
    "baseUrl": ".",
    "paths": {
      "@devian/core": ["./devian-core/src"],
      "@devian/protobuf": ["./devian-protobuf/src"]
    }
  }
}
```

### 번들러 설정

번들러(webpack, vite, esbuild 등) 사용 시 동일한 alias 설정이 필요하다.

**Vite 예시 (vite.config.ts):**
```typescript
export default {
  resolve: {
    alias: {
      '@devian/core': path.resolve(__dirname, 'framework/ts/devian-core/src'),
      '@devian/protobuf': path.resolve(__dirname, 'framework/ts/devian-protobuf/src')
    }
  }
}
```

---

## C# NuGet Dependencies

generated C# 코드는 `netstandard2.1`을 타겟으로 하며, 일부 NuGet 패키지에 대한 명시적 참조가 필요하다.

### 필수 패키지

| 패키지 | 버전 | 용도 | 적용 대상 |
|--------|------|------|-----------|
| `Newtonsoft.Json` | 13.0.3 | JSON 직렬화/역직렬화 | Protocol, Domain 모듈 |

### 이유

Unity는 `System.Text.Json`을 기본 제공하지 않으므로, Unity 호환성을 위해 **Newtonsoft.Json**을 사용한다.
Unity는 `com.unity.nuget.newtonsoft-json` 패키지로 Newtonsoft.Json을 기본 제공한다.

### 자동 생성 csproj 예시

빌드 도구가 생성하는 Protocol 모듈의 csproj:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>netstandard2.1</TargetFramework>
    <LangVersion>9.0</LangVersion>
    <Nullable>enable</Nullable>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Newtonsoft.Json" Version="13.0.3" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\..\module\Devian.Core\Devian.Core.csproj" />
    <ProjectReference Include="..\..\module\Devian.Network\Devian.Network.csproj" />
    <ProjectReference Include="..\Devian.Module.Common\Devian.Module.Common.csproj" />
  </ItemGroup>
</Project>
```

---

## Must / Must Not

MUST

- generated를 import하는 "수동 코드(manual)"는 generated와 분리된 폴더에서 관리한다.
- input_common.json의 csConfig/tsConfig/dataConfig 설계로 산출 충돌을 방지한다.
- TypeScript 프로젝트는 paths alias를 설정하여 @devian/core 등을 import한다.

MUST NOT

- generated 파일을 직접 패치해서 '임시로' 문제를 해결하지 않는다.

---

## Reference

- Policy SSOT: `skills/devian/03-ssot/SKILL.md`
- 동작 정본: 런타임/제너레이터 코드
