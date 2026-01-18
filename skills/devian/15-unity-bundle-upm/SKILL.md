# 15-unity-bundle-upm

Status: ACTIVE  
AppliesTo: v10

## SSOT

이 문서는 UnityExample embedded 패키지들의 **레이아웃/의존성/asmdef 규약(정책)**을 정의한다.

- 구현 및 공개 API는 **코드가 정답**이며, 문서의 예시는 참고다.
- 코드 변경 시 문서를 SSOT로 맞추지 않는다(필요하면 참고 수준으로 갱신).

---

## 목표

- UnityEngine.dll을 외부 .NET 빌드에서 직접 참조하지 않는다.
- UnityExample에 embedded UPM 패키지로 다음을 제공한다:
  - `com.devian.core` (Devian.Core 소스)
  - `com.devian.network` (Devian.Network 소스)
  - `com.devian.protobuf` (Devian.Protobuf 소스 + Google.Protobuf.dll 동봉)
  - `com.devian.module.common` (Devian.Module.Common 소스: Common features 포함)
  - `com.devian.unity.network` (Unity 어댑터: WebSocketClientBehaviourBase)
  - `com.devian.unity.common` (Unity 어댑터: Devian.Module.Common 확장, UnityLogSink, TableID Editor)
- 설치 경험:
  - 필요한 패키지를 직접 embedded로 추가한다 (예: `com.devian.unity.common`, `com.devian.unity.network`, `com.devian.module.common` 등).

## 비목표

- TS 변경 없음.
- UPM 배포(레지스트리/서버)는 다루지 않는다(embedded만).
- WebGL 지원을 포함한다. (WebSocket.jslib + WebGLWsDriver + WebSocketClient 분기)

---

## 패키지 루트 (embedded)

모든 패키지는 아래에 위치한다:

```
framework-cs/apps/UnityExample/Packages/
```

## 구성 패키지 목록

| 패키지 | 역할 |
|--------|------|
| `com.devian.core` | Devian.Core 소스 |
| `com.devian.network` | Devian.Network 소스 |
| `com.devian.protobuf` | Devian.Protobuf 소스 + Google.Protobuf.dll |
| `com.devian.module.common` | Devian.Module.Common 소스 (Common features 포함) |
| `com.devian.unity.network` | Unity 어댑터 (WebSocketClientBehaviourBase) |
| `com.devian.unity.common` | Unity 어댑터 (Devian.Module.Common 확장: UnityLogSink, TableID Editor) |

## 버전 정책

모든 `com.devian.*` 패키지는 동일한 버전 문자열을 사용한다. (예: `0.1.0`)

---

## asmdef 규약 (핵심)

| asmdef | name | references | 기타 |
|--------|------|------------|------|
| `Devian.Core.asmdef` | `Devian.Core` | `[]` | - |
| `Devian.Network.asmdef` | `Devian.Network` | `["Devian.Core"]` | excludePlatforms: [] |
| `Devian.Protobuf.asmdef` | `Devian.Protobuf` | `["Devian.Core"]` | precompiled: Google.Protobuf.dll |
| `Devian.Module.Common.asmdef` | `Devian.Module.Common` | `["Devian.Core"]` | - |
| `Devian.Unity.Network.asmdef` | `Devian.Unity.Network` | `["Devian.Network"]` | excludePlatforms: [] |
| `Devian.Unity.Common.asmdef` | `Devian.Unity.Common` | `["Devian.Module.Common"]` | - |

---

## Google.Protobuf 의존성 규약 (중요)

Unity는 NuGet 패키지 복원을 기본 제공하지 않으므로:

- `com.devian.protobuf` 패키지 내에 `Runtime/Plugins/Google.Protobuf.dll`을 동봉한다.
- 버전은 `Devian.Protobuf.csproj`와 동일(예: 3.25.1)을 사용한다.

**DLL이 없으면 Unity 컴파일 실패.**

DLL 확보 방법:
1. 로컬 NuGet 캐시: `~/.nuget/packages/google.protobuf/3.25.1/lib/netstandard2.0/Google.Protobuf.dll`
2. 빌드 산출물에서 복사
3. NuGet에서 직접 다운로드

---

## 소스 기준 (중요)

Unity UPM 패키지의 소스는 `framework-cs/modules/*`의 소스를 **복사하여 포함**한다:

| 원본 | 대상 |
|------|------|
| `Devian.Core/src/**` | `com.devian.core/Runtime/**` |
| `Devian.Network/src/**` | `com.devian.network/Runtime/**` |
| `Devian.Protobuf/src/**` | `com.devian.protobuf/Runtime/**` |
| `Devian.Module.Common/generated/**` | `com.devian.module.common/Runtime/**` |
| `Devian.Module.Common/features/**` | `com.devian.module.common/Runtime/Features/**` |

(추후 단일 소스화는 별도 SKILL에서 다룬다.)

---

## 금지

- UnityEngine.dll을 외부 .NET 프로젝트에서 직접 참조해 DLL 빌드하는 방식 금지.
- `com.devian.unity` 메타 패키지 생성/유지 금지 (deprecated).

---

## Reference

- Related: `skills/devian/14-unity-network-client-upm/SKILL.md`
- Related: `skills/devian/19-unity-module-common-upm/SKILL.md`
- Related: `skills/devian/21-unity-common-upm/SKILL.md`
- Core modules: `framework-cs/modules/Devian.*/`
