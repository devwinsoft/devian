# Devian Reference (Code-Derived)

GeneratedFrom: framework/cs/Devian.Tools + framework/cs/Devian.* + input/build/build.json  
Snapshot: devian-v10-wireframe

> 이 문서는 “코드가 실제로 가정/생성/요구하는 것”을 요약한 Reference다.
> 정책/규약은 `skills/devian/03-ssot/SKILL.md`가 정본이며, 충돌 시 이 문서가 **구현 기준 정답**이다.

---

## 1) build.json (Schema Summary)

Devian.Tools의 BuildConfig 모델 기준 스키마(코드 헤더에 v9 표기) 요약:

```json
{
  "version": "v10",
  "tempDir": "temp",
  "domains": {
    "{DomainKey}": {
      "contractsDir": "input/{DomainKey}/contracts",
      "contractFiles": ["*.json"],
      "tablesDir": "input/{DomainKey}/tables",
      "tableFiles": ["*.xlsx"],
      "csTargetDirs": ["modules/cs/{DomainKey}/generated"],
      "tsTargetDirs": ["modules/ts/{DomainKey}/generated"],
      "upmTargetDirs": [],
      "dataTargetDirs": ["data/{DomainKey}"]
    }
  },
  "protocols": {
    "{LinkName}": {
      "protocolsDir": "input/{LinkName}/protocols",
      "protocolFile": "{ProtocolName}.json",
      "csTargetDirs": ["modules/cs/{LinkName}/generated"],
      "tsTargetDirs": ["modules/ts/{LinkName}/generated"]
    }
  }
}
```

### Copy Semantics (중요)

Devian.Tools는 각 DomainKey/LinkName 처리 단위마다 targetDir을 **clean 후 copy**한다.
즉, 서로 다른 항목이 같은 targetDir을 공유하면 마지막 항목이 이전 산출물을 지울 수 있다.

---

## 2) Staging Output Layout

`tempDir` 아래 staging 구조는 다음과 같다.

### DATA (DomainKey)

- `{tempDir}/{DomainKey}/cs/generated/**`
- `{tempDir}/{DomainKey}/ts/generated/**`
- `{tempDir}/{DomainKey}/data/json/**.ndjson`

### PROTOCOL (LinkName)

- `{tempDir}/{LinkName}/cs/generated/**`
- `{tempDir}/{LinkName}/ts/generated/**`

---

## 3) DATA Codegen

### 3.1 ContractGen

- 입력: `contractsDir` 아래 `*.json`
- 출력 파일명: `{ContractFileBase}.g.cs`, `{ContractFileBase}.g.ts`
- C# namespace: `Devian.{DomainKey}`

Contract JSON 모델은 `Devian.Tools.Models.ContractSpec`를 따른다.

### 3.2 TableGen

- 입력: `tablesDir` 아래 `*.xlsx`
- 출력(테이블 1개당):
  - C# row: `{TableName}Row.g.cs`
  - C# container(PrimaryKey가 있을 때): `TB_{TableName}.g.cs`
  - TS row: `{TableName}Row.g.ts`
  - NDJSON: `{tableNameLower}.ndjson`

테이블 헤더/옵션/중단 규칙은 `TableSchemaParser` 구현과 동일하며, 정책 문서는 `skills/devian/24-table-authoring-rules/SKILL.md`를 참고.

---

## 4) PROTOCOL Codegen

### 4.1 ProtocolSpec (JSON)

`Devian.Tools.Models.ProtocolSpec` 모델을 따른다.

- `namespace` : 선택(optional). 존재 시 파일명 base와 일치해야 함
- `direction` : 문자열 (`client_to_server`, `server_to_client`, `bidirectional`)
- `messages[]` : `{ name, opcode?, fields[] }`
- `fields[]` : `{ name, type, optional, comment?, tag? }`

### 4.2 Registry Files

프로토콜 디렉토리(`protocolsDir`)에 아래 파일을 유지한다.

- `{ProtocolName}.opcodes.json`
- `{ProtocolName}.tags.json`

Devian.Tools는 빌드 시 레지스트리를 읽고/갱신하여 저장한다.

### 4.3 Frame Format

생성 코드가 가정하는 프레임 포맷 요약:

- `[opcode:int32LE][payload...]`

참고: C# 생성물은 `BitConverter`를 사용하며 런타임 엔디안에 의존한다(일반적으로 little-endian 환경을 전제로 함).

### 4.4 C# Generated Shape (요약)

ProtocolName 단위로 하나의 `.g.cs`가 생성된다.

- `namespace Devian.Protocol`
- `public static partial class {ProtocolName}` 내부에
  - `Opcodes` 상수
  - 메시지 타입(클래스)
  - codec(JSON + Protobuf-style)
  - 수신 라우터/스텁(추상 base)
  - 송신 프록시(Proxy)

또한 ProtocolName 생성물은 **자체 송신 계약**을 가진다.

- `public interface ISender { void SendTo(int sessionId, ReadOnlySpan<byte> frame); }`

### 4.5 TypeScript Generated Shape (요약)

ProtocolName 단위로 하나의 `.g.ts`가 생성된다.

- `export namespace {ProtocolName}` 내부에
  - `Opcodes` 상수
  - 메시지 타입(interface)
  - codec(JSON + Protobuf-style)
  - `Stub` (handler 등록 + dispatch)
  - `Proxy` (send* 메서드)
  - `SendFn` 타입: `(sessionId:number, frame:Uint8Array) => void|Promise<void>`

---

## 5) Transport Runtime (Devian.Network)

현 스냅샷에는 `framework/cs/Devian.Network`가 별도 transport 계약을 제공한다.

- `IPacketSender` : `SendAsync(PacketEnvelope, CancellationToken)`
- `PacketEnvelope` : `{ SessionId: long, Payload: byte[] }`

프로토콜 생성물이 요구하는 `ISender`/`SendFn`과는 별개이며,
프로젝트에서는 이 둘을 연결하는 adapter 계층이 필요하다.

---

## 6) Reference vs Policy

- 정책/검증/경로 규약: `skills/devian/03-ssot/SKILL.md`
- 구현/시그니처/산출 구조: 이 문서
