# Network Sample

WebSocket 클라이언트 샘플.

- **namespace:** `Devian`
- **assembly (asmdef):** Devian Templates Network

## 의존성

이 샘플을 사용하려면 다음 패키지가 필요합니다:
- `com.devian.foundation`
- `com.devian.protocol.game`

참조하는 어셈블리:
- `Devian.Core`
- `Devian.Unity`
- `Devian.Protocol.Game`

## 파일 구조

```
Network/
├── Runtime/
│   ├── [asmdef: Devian Templates Network]
│   └── EchoWsClientSample.cs
├── Editor/
│   ├── [asmdef: Devian Templates Network Editor]
│   └── EchoWsClientSampleEditor.cs
└── README.md
```

## 사용법

1. `EchoWsClientSample`을 GameObject에 추가
2. TS GameServer 실행: `cd framework-ts/apps/GameServer && npm start`
3. Play 모드에서 Inspector 버튼 사용

## 커스터마이징

이 코드는 프로젝트 소유입니다. 자유롭게 수정하세요:
- namespace, 클래스명 변경
- 다른 프로토콜로 교체
- 비즈니스 로직 추가
