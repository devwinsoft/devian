# Devian – 01 Core Philosophy

## 1. Devian의 정체성

**Devian은 프레임워크다.**

Devian은 기존 Devarc 프레임워크를 재설계·재구성한 차세대 프레임워크이며,
소규모 팀이 하나의 규약으로 서버·클라이언트·엔진 계층을 동시에 구성할 수 있도록 설계되었다.

- Devian은 라이브러리가 **아니다**
- Devian은 단순한 도구 모음이 **아니다**
- Devian은 인프라 제품이 **아니다**

**Devian은 빌드 규약과 확장 모델을 중심으로 정의되는 프레임워크다.**

---

## 2. Devian의 단일 목표

> **"한 번의 빌드로, 코드·데이터·프로토콜을 생성하고
> 동일한 정의를 서버·클라이언트·엔진 전체에서 공유한다."**

이 목표를 위해 Devian은:

- 정의를 분산하지 않고
- 중복 생성을 허용하지 않으며
- 런타임별 해석 차이를 최소화한다

---

## 3. Devian의 설계 원칙

### 3.1 단일 정의 (Single Source of Truth)

- 모든 코드와 데이터의 출발점은 정의 파일이다
- 수작업 구현은 예외가 아니라 **규약 위반**이다

### 3.2 단일 빌드 흐름

- 빌드는 **하나의 명령**으로 수행된다
- 서버/클라이언트/엔진은 빌드 결과의 **소비자**일 뿐이다

### 3.3 복잡성 제거

- 프레임워크는 문제를 해결하지 않는다
- **문제를 단순화할 수 없는 구조를 허용하지 않는다**

### 3.4 확장은 분기가 아니라 Skill

- Devian은 커지지 않는다
- **Devian은 확장된다**
- 확장은 항상 **Skill 단위**로 이루어진다

---

## 4. Framework 경계

### Framework가 책임지는 것

| 항목 | 설명 |
|------|------|
| 정의 포맷 | contracts, tables, protocols JSON 스키마 |
| 빌드 규약 | build.json, 도메인 구조, 빌드 흐름 |
| 코드/데이터 생성 규칙 | codegen 출력 형태, 네임스페이스 규칙 |
| Skill 로딩 및 연결 모델 | Skill 인터페이스, 확장 지점 |

### Framework가 포함하지 않는 것 (Skill 영역)

| 항목 | 담당 Skill |
|------|-----------|
| 특정 서버 구현 | NestJS Server Skill |
| 특정 엔진 로직 | Unity Skill |
| 특정 네트워크 스택 | C# Network Client Skill |

---

## 5. Devian의 완성 형태

```
서버, 클라이언트, 엔진이
서로 다른 언어와 런타임 위에 존재하더라도
동일한 정의와 동일한 빌드 결과를 공유한다
```

**Devian은 이를 가능하게 하는 프레임워크다.**

---

## Hard Rules (MUST)

| # | Rule |
|---|------|
| 1 | Devian은 **프레임워크**다 |
| 2 | Source of truth는 **`input/`**이며, 빌드는 **`build.json`**을 따른다 |
| 3 | 모든 확장은 **Skill**로 정의한다 |
| 4 | Framework에 런타임 구현을 직접 추가하지 않는다 |

---

## Soft Rules (SHOULD)

| # | Rule |
|---|------|
| 1 | 스펙은 언어 중립(JSON)으로 시작한다 |
| 2 | 빌드는 결정적(deterministic)이어야 한다 |
| 3 | Skill은 독립적으로 추가·제거 가능해야 한다 |

---

## 용어 규칙

### 필수 표현

| 표현 | 용도 |
|------|------|
| "Devian Framework" | 공식 명칭 |
| "Skill" | 확장 단위 |
| "빌드 규약" | Framework 핵심 |

### 금지 표현

| 표현 | 금지 이유 |
|------|----------|
| "Devian은 프레임워크가 아니다" | 철학 위반 |
| "인프라", "도구 모음" | 정체성 오해 |
| "현재 구현 상태" | 완성형 기준 논의만 |

---

## Related Skills

| Skill | 관계 |
|-------|------|
| `00-rules-minimal` | Hard Rules 정의 |
| `02-skill-specification` | Skill 공식 스펙 |
| `60-build-pipeline` | Build 규약 |
| `10-core-runtime` | Core runtime |

---

## Version History

| Version | Date | Changes |
|---------|------|---------|
| 1.0.0 | 2025-12-28 | Initial |
