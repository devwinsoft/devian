# 20-save-system — Cloud Save Guide (Quick Start)


Status: ACTIVE
AppliesTo: v10


---


## 1. 목적


이 문서는 Devian Cloud Save를 **프로젝트에 붙이는 최소 절차**만 다룬다.


- API 계약: [20-cloudsave-client-api](../20-cloudsave-client-api/SKILL.md)
- Android(GPGS) 구현/정책: [21-cloudsave-google](../21-cloudsave-google/SKILL.md)
- 공통 스펙/충돌/암호화: [10-cloudsave-spec](../10-cloudsave-spec/SKILL.md)


---


## 2. 빠른 체크리스트


- (Unity) `com.devian.samples` 설치 (CloudSave/LocalSave 구현은 `Samples~/CloudClientSystem`에 포함)
- (Android) Google Play Games Plugin for Unity 설치 및 프로젝트 설정
- (Runtime) `CloudSaveManager.Instance.Configure(...)` 호출
- (Runtime) 필요 시 `SignInIfNeededAsync` 호출 후 Save/Load 수행
- (Product) Editor/Guest는 Firebase Anonymous를 사용한다. 실기기에서는 Guest + Google(Android) + Apple(iOS)을 지원한다. 상세는 [login-manager](../../../devian-unity/90-samples/36-samples-login-manager/SKILL.md) 참조. login-manager는 Save System 샘플 모듈에 포함되어 동일 asmdef로 컴파일된다.
- (Product) 기본 Cloud Save 저장소는 플랫폼별(Android=GPGS / iOS=iCloud)로 유지한다. iOS(iCloud) 구현이 준비되지 않은 단계에서는 Firebase 구현을 임시로 사용할 수 있다.
- (Out of scope) 기타 소셜 로그인 제공자는 이 스킬 범위 밖이다.


---


## 3. Android (Google Play Games) 구성 요약


Devian은 Samples(ClaudSave)에 `GoogleCloudSaveClient`를 제공한다.


- Reflection 기반: 플러그인이 없어도 컴파일은 됨
- 실제 동작 조건:
  - Android 기기 런타임
  - GPGS 플러그인 설치 + 초기화/설정 완료
  - 로그인 가능 상태


주의:
- Firebase Auth를 사용하더라도, GPGS Saved Games는 Play Games 로그인 계정 기준으로 동작한다.
- iOS(iCloud)와 Android(GPGS) 간 자동 세이브 이전/통합은 비목표다.


구현 상세는 [21-cloudsave-google](../21-cloudsave-google/SKILL.md) 참고.


---


## 4. Configure 예시


```csharp
using System.Collections.Generic;
using UnityEngine;


namespace Devian
{
    public sealed class CloudSaveBootstrap : MonoBehaviour
    {
        [SerializeField] private bool useEncryption = true;


        private void Awake()
        {
            var slots = new List<CloudSaveSlot>
            {
                new CloudSaveSlot { slotKey = "main", cloudSlot = "main" }
            };


            var client = new GoogleCloudSaveClient();


            CloudSaveManager.Instance.Configure(
                client: client,
                useEncryption: useEncryption,
                slots: slots);
        }
    }
}
```


---


## 5. Save / Load 최소 예시


```csharp
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;


namespace Devian
{
    public sealed class CloudSaveExample : MonoBehaviour
    {
        public async Task SaveAsync(CancellationToken ct)
        {
            // 권장: 필요 시 로그인 먼저
            await CloudSaveManager.Instance.SignInIfNeededAsync(ct);


            string payloadJson = "{\"coins\":123,\"stage\":5}";
            await CloudSaveManager.Instance.SaveAsync(slot: "main", payload: payloadJson, ct: ct);
        }


        public async Task<string> LoadAsync(CancellationToken ct)
        {
            await CloudSaveManager.Instance.SignInIfNeededAsync(ct);


            var r = await CloudSaveManager.Instance.LoadPayloadAsync(slot: "main", ct: ct);
            return r.IsSuccess ? r.Value : null;
        }
    }
}
```


---


## 6. 샘플 위치


`com.devian.samples/Samples~/CloudClientSystem`


- `ClaudSaveInstaller.InitializeAsync(CancellationToken ct)` — 공통 엔트리(내부 플랫폼 분기)
- `ClaudSaveInstaller.InitializeAsync(List<CloudSaveSlot>, bool, CancellationToken)` — slots/encryption 포함 오버로드
- (옵션) `FirebaseCloudSaveClient` — iOS에서 iCloud 구현이 준비되지 않은 경우 임시로 사용 가능
- (문서) Firebase 구현 상세: [25-cloudsave-firebase](../25-cloudsave-firebase/SKILL.md)


---


## 7. 운영 팁 (필수 아님)


- 저장 트리거는 서비스 레이어 책임(스테이지 클리어/설정 변경/백그라운드 진입 등)
- 네트워크 실패 재시도/디바운스/충돌 정책은 게임 요구사항에 맞게 별도 설계
