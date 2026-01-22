# Network Template

WebSocket 클라이언트 템플릿.

## 의존성

이 템플릿을 사용하려면 다음 패키지가 필요합니다:
- `com.devian.core`
- `com.devian.network`
- `com.devian.unity.network`
- `com.devian.protocol.sample`

## 파일 구조

```
Network/
├── Runtime/
│   ├── Devian.Templates.Network.asmdef
│   └── EchoWsClientSample.cs
├── Editor/
│   ├── Devian.Templates.Network.Editor.asmdef
│   └── EchoWsClientSampleEditor.cs
└── README.md
```

## 사용법

1. `EchoWsClientSample`을 GameObject에 추가
2. TS SampleServer 실행: `cd framework-ts/apps/SampleServer && npm start`
3. Play 모드에서 Inspector 버튼 사용

## 커스터마이징

이 코드는 프로젝트 소유입니다. 자유롭게 수정하세요:
- namespace, 클래스명 변경
- 다른 프로토콜로 교체
- 비즈니스 로직 추가
