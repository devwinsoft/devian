# 10 — Network Template

> **패키지:** com.devian.templates  
> **샘플명:** Network  
> **도메인:** devian-templates  
> **문서 버전:** v3

---

## 1. 개요

WebSocket 클라이언트 템플릿. `com.devian.templates` 패키지의 Samples~에 포함.

**용도:**
- WebSocket 기반 네트워크 클라이언트 구현 시작점
- Protocol 연동 예시 (Sample 프로토콜)
- Unity Inspector 통합 예시

---

## 2. 경로

### 2.1 원본 (upm-src)

```
framework-cs/upm-src/com.devian.templates/Samples~/Network/
```

### 2.2 설치 후 위치 (Unity 프로젝트)

```
Assets/Samples/Devian Templates/0.1.0/Network/
```

---

## 3. 폴더 구조

```
Samples~/Network/
├── Runtime/
│   ├── Devian.Templates.Network.asmdef
│   └── EchoWsClientSample.cs
├── Editor/
│   ├── Devian.Templates.Network.Editor.asmdef
│   └── EchoWsClientSampleEditor.cs
└── README.md
```

---

## 4. asmdef 정보

### 4.1 Runtime asmdef

**파일명:** `Devian.Templates.Network.asmdef`

```json
{
  "name": "Devian.Templates.Network",
  "rootNamespace": "Devian.Templates.Network",
  "references": [
    "Devian.Core",
    "Devian.Network",
    "Devian.Unity.Network",
    "Devian.Protocol.Sample"
  ]
}
```

### 4.2 Editor asmdef

**파일명:** `Devian.Templates.Network.Editor.asmdef`

```json
{
  "name": "Devian.Templates.Network.Editor",
  "rootNamespace": "Devian.Templates.Network.Editor",
  "references": [
    "Devian.Templates.Network",
    "Devian.Unity.Network"
  ],
  "includePlatforms": ["Editor"]
}
```

---

## 5. 의존성

이 템플릿을 사용하려면 다음 패키지가 프로젝트에 설치되어 있어야 함:

| 패키지 | 필수 | 용도 |
|--------|------|------|
| `com.devian.core` | ✅ | 핵심 인터페이스 |
| `com.devian.network` | ✅ | 네트워크 런타임 |
| `com.devian.unity.network` | ✅ | Unity 네트워크 클라이언트 |
| `com.devian.protocol.sample` | ✅ | Sample 프로토콜 (테스트용) |

---

## 6. 포함 파일

### 6.1 EchoWsClientSample.cs

WebSocket 클라이언트 샘플 구현.

**기능:**
- 서버 연결/해제
- Ping/Echo 메시지 전송
- Protocol Stub/Proxy 연동

**namespace:** `Devian.Templates.Network`

### 6.2 EchoWsClientSampleEditor.cs

Unity Inspector 확장.

**기능:**
- Connect/Disconnect 버튼
- Send Ping/Echo 버튼
- 연결 상태 표시

**namespace:** `Devian.Templates.Network.Editor`

---

## 7. 설치 방법

1. Unity 프로젝트 열기
2. Window → Package Manager
3. `Devian Templates` 패키지 선택
4. Samples 섹션에서 "Network" → "Import" 클릭

---

## 8. 테스트 실행

1. TS SampleServer 실행:
   ```bash
   cd framework-ts/apps/SampleServer
   npm start
   ```

2. Unity에서:
   - `EchoWsClientSample` 컴포넌트를 GameObject에 추가
   - Play 모드 진입
   - Inspector에서 Connect → Send Ping/Echo

---

## 9. 커스터마이징

### 9.1 코드 수정

설치된 샘플은 `Assets/Samples/`에 위치하므로 자유롭게 수정 가능:

```csharp
// 1) namespace 변경
namespace MyGame.Network
{
    // 2) 클래스명 변경
    public class MyNetworkClient : WebSocketClientBehaviourBase
    {
        // 3) 자신의 Protocol 사용
        private MyProtocol.C2Server.Proxy _proxy;
        
        protected override void OnConnected()
        {
            _proxy = new MyProtocol.C2Server.Proxy(this);
        }
    }
}
```

### 9.2 Protocol 변경

`Devian.Protocol.Sample` 대신 자신의 프로토콜 사용:

1. asmdef references 수정
2. using 문 변경
3. Proxy/Stub 타입 변경

---

## 10. 참고

- 정책 문서: `skills/devian-templates/01-templates-policy/SKILL.md`
- Network SDK: `skills/devian/12-network-ws-client/SKILL.md`
- Protocol 코드젠: `skills/devian/22-codegen-protocol-csharp-ts/SKILL.md`
