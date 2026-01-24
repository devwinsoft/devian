# 15-unity-bundle-upm

Status: ACTIVE  
AppliesTo: v10

## Prerequisites

**Unity C# 문법 제한:** 이 문서에서 다루는 모든 UPM 패키지 코드는 `skills/devian/04-unity-csharp-compat/SKILL.md`를 준수한다 (금지 문법 사용 시 FAIL).

## SSOT

이 문서는 UnityExample embedded 패키지들의 **레이아웃/의존성/asmdef 규약(정책)**을 정의한다.

- 구현 및 공개 API는 **코드가 정답**이며, 문서의 예시는 참고다.
- 코드 변경 시 문서를 SSOT로 맞추지 않는다(필요하면 참고 수준으로 갱신).

---

## 목표

- UnityEngine.dll을 외부 .NET 빌드에서 직접 참조하지 않는다.
- UnityExample에 embedded UPM 패키지로 다음을 제공한다:
  - `com.devian.core` (Devian 런타임 통합: Core + Network + Protobuf)
  - `com.devian.module.common` (Devian.Module.Common 소스: Common features 포함)
  - `com.devian.unity.network` (Unity 어댑터: NetWsClientBehaviourBase)
  - `com.devian.unity.common` (Unity 어댑터: Devian.Module.Common 확장, UnityLogSink, TableID Editor)
- 설치 경험:
  - 필요한 패키지를 직접 embedded로 추가한다 (예: `com.devian.unity.common`, `com.devian.unity.network`, `com.devian.module.common` 등).

## 비목표

- TS 변경 없음.
- UPM 배포(레지스트리/서버)는 다루지 않는다(embedded만).
- WebGL 지원을 포함한다. (WebSocket.jslib + WebGLWsDriver + NetWsClient 분기)

---

## 패키지 루트 (embedded)

모든 패키지는 아래에 위치한다:

```
framework-cs/apps/UnityExample/Packages/
```

## 구성 패키지 목록

| 패키지 | 역할 |
|--------|------|
| `com.devian.core` | Devian 런타임 통합 (Core + Network + Protobuf) |
| `com.devian.module.common` | Devian.Module.Common 소스 (Common features 포함) |
| `com.devian.unity.network` | Unity 어댑터 (NetWsClientBehaviourBase) |
| `com.devian.unity.common` | Unity 어댑터 (Devian.Module.Common 확장: UnityLogSink, TableID Editor) |

> **패키지 단일화 정책 (Hard Rule):**
> - 이전의 `com.devian.network`, `com.devian.protobuf` 패키지는 삭제되었다.
> - 모든 런타임 기능은 `com.devian.core` 단일 패키지에 포함된다.

## 버전 정책

모든 `com.devian.*` 패키지는 동일한 버전 문자열을 사용한다. (예: `0.1.0`)

---

## com.devian.core 구조 (Hard Rule)

com.devian.core 패키지는 **단일 어셈블리**를 제공한다:

```
com.devian.core/
├── package.json
├── README.md
└── Runtime/
    ├── Devian.Core.asmdef
    ├── Core/
    │   └── *.cs (Core 소스 - namespace: Devian)
    ├── Net/
    │   └── *.cs (Network 소스 - namespace: Devian, Net* 접두사 타입)
    └── Proto/
        └── *.cs (Protobuf 소스 - namespace: Devian, Dff*/Protobuf*/IProto* 타입)
```

> **namespace 단일화 정책:**
> - 모든 런타임 코드는 `namespace Devian` 단일을 사용한다.
> - 네트워크 타입은 `Net` 접두사로 구분한다 (예: `NetClient`, `NetWsClient`, `NetHttpRpcClient`)
> - Proto 타입은 기존 이름을 유지한다 (예: `DffValue`, `DffConverter`, `IProtoEntity`)

> **asmdef 단일화 정책:**
> - `com.devian.core`는 `Devian.Core` 하나의 asmdef만 제공한다.
> - 다른 패키지가 Devian 런타임을 참조할 때는 `"Devian.Core"`만 references에 추가한다.

---

## asmdef 규약 (핵심)

| asmdef | name | references | 패키지 |
|--------|------|------------|--------|
| `Devian.Core.asmdef` | `Devian.Core` | `[]` | com.devian.core |
| `Devian.Module.Common.asmdef` | `Devian.Module.Common` | `["Devian.Core"]` | com.devian.module.common |
| `Devian.Unity.Network.asmdef` | `Devian.Unity.Network` | `["Devian.Core"]` | com.devian.unity.network |
| `Devian.Unity.Common.asmdef` | `Devian.Unity.Common` | `["Devian.Core", "Devian.Module.Common"]` | com.devian.unity.common |

---

## 의존성 규약

| 패키지 | dependencies |
|--------|--------------|
| `com.devian.core` | (없음) |
| `com.devian.module.common` | `com.devian.core` |
| `com.devian.unity.network` | `com.devian.core`, `com.devian.module.common` |
| `com.devian.unity.common` | `com.devian.core`, `com.devian.module.common` |

---

## SSOT 경로

| 유형 | 경로 |
|------|------|
| 정적 UPM 패키지 (SSOT) | `framework-cs/upm/` |
| 생성 UPM 패키지 | `framework-cs/upm-gen/` |
| UnityExample 최종 패키지 | `framework-cs/apps/UnityExample/Packages/` |

---

## 패키지 동기화 규칙 (Hard Rule)

**UnityExample/Packages는 빌더가 clean+copy로 갱신한다.**

| 정본 | 복사본 | 동작 |
|------|--------|------|
| `upm/{pkg}` 또는 `upm-gen/{pkg}` | `Packages/{pkg}` | clean + copy |

**수정은 upm/upm-gen에서만 한다.**

- `Packages/`에서 수정한 코드는 다음 sync에서 덮어써지며, **정책 위반**이다.
- 수동 패키지(`com.devian.core`, `com.devian.unity.network` 등)는 `upm/`에서 수정
- 생성 패키지(`com.devian.module.common`, `com.devian.protocol.*` 등)는 빌더가 `upm-gen/`에 생성

**소스 우선순위 (sync 시):**
1. `upm-gen/{pkg}` 존재 → upm-gen에서 복사
2. `upm-gen/{pkg}` 없음 → upm에서 복사

**수동 sync 절차 (빌더 없이):**
```bash
# staticUpmPackages는 upm-gen에서 복사 (빌더가 upm → upm-gen 복사 후 생성물 추가)
for pkg in com.devian.templates com.devian.unity.common com.devian.unity.network; do
    rm -rf Packages/$pkg && cp -r upm-gen/$pkg Packages/$pkg
done

# upm에만 있는 패키지 (upm-gen에 없는 것)
for pkg in com.devian.core; do
    rm -rf Packages/$pkg && cp -r upm/$pkg Packages/$pkg
done
```

---

## 마이그레이션 노트

이전에 `com.devian.network`, `com.devian.protobuf`를 사용하던 프로젝트는:
1. 해당 패키지 폴더를 삭제한다.
2. `com.devian.core`로 교체한다.
3. 코드에서 모든 using을 `using Devian;`으로 통일한다.
4. 네트워크 타입을 `Net` 접두사로 변경한다:
   - `NetworkClient` → `NetClient`
   - `WebSocketClient` → `NetWsClient`
   - `HttpRpcClient` → `NetHttpRpcClient`
