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


- (Unity) `com.devian.foundation` 설치
- (Android) Google Play Games Plugin for Unity 설치 및 프로젝트 설정
- (Runtime) `CloudSaveManager.Instance.Configure(...)` 호출
- (Runtime) 필요 시 `SignInIfNeededAsync` 호출 후 Save/Load 수행


---


## 3. Android (Google Play Games) 구성 요약


Devian은 Foundation에 `GooglePlayGamesCloudSaveClient`를 제공한다.


- Reflection 기반: 플러그인이 없어도 컴파일은 됨
- 실제 동작 조건:
  - Android 기기 런타임
  - GPGS 플러그인 설치 + 초기화/설정 완료
  - 로그인 가능 상태


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


            var client = new GooglePlayGamesCloudSaveClient();


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


`com.devian.samples/Samples~/CloudSave-GPGS`


- `GpgsCloudSaveInstaller.ConfigureCloudSave(object googlePlayService)` — 클라이언트 주입 전용
- `GpgsCloudSaveInstaller.ConfigureCloudSave(object, List<CloudSaveSlot>, bool)` — 레거시 호환
- `GpgsCloudSaveClient` — 커스텀 구현 레퍼런스용 스텁


---


## 7. 운영 팁 (필수 아님)


- 저장 트리거는 서비스 레이어 책임(스테이지 클리어/설정 변경/백그라운드 진입 등)
- 네트워크 실패 재시도/디바운스/충돌 정책은 게임 요구사항에 맞게 별도 설계
