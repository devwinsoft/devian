# Auth-Firebase


Firebase Authentication 샘플이다. (Anonymous/Google/Apple/Facebook 공통 흐름: Firebase 초기화 + Sign-in + ID token 획득)


- Firebase 초기화 / 의존성 체크
- Sign-in (Anonymous는 바로 실행 가능)
- (Google/Apple/Facebook) Provider 토큰을 받아 Firebase credential 로그인에 연결하는 흐름
- ID token 획득


## Requirements
- Unity 프로젝트에 Firebase Unity SDK가 설치되어 있어야 한다.
  - 최소: FirebaseApp + FirebaseAuth
- (Android 테스트) `google-services.json`이 필요하다.


## Android Test Checklist
1) Firebase Console에서 Android 앱 등록
2) `google-services.json`을 Unity 프로젝트 `Assets/` 아래에 추가
3) Authentication → Sign-in method에서 사용할 로그인 제공자(Anonymous/Google/Apple 등)를 활성화한다.
4) `AuthFirebaseSample`를 빈 GameObject에 붙여 실행
5) 로그에서 초기화 성공 / 로그인 성공 / UID 출력 확인


## iOS
- 이 단계에서는 iOS 테스트를 하지 않는다(구조만 유지).
