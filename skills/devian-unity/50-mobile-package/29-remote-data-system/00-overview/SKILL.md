# 29-remote-data-system — Overview

Status: ACTIVE
AppliesTo: v10

RemoteData System은 MobilePackage의 버전 체크와 서버 UTC 기준 시각을 단일 경로로 제공한다.
`MobileApplication`이 버전 체크 URL을 소유하고, `RemoteDataManager`가 실제 판정/동기화를 수행한다.
`LoginManager`는 로그인 시작 시 `RemoteDataManager.InitializeAsync`를 가장 먼저 호출하고, 중복 구현을 갖지 않는다.

---

## Sub-skills

- [10-remote-data-manager](../10-remote-data-manager/SKILL.md)
