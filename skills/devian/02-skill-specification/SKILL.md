# Devian – 02 Skill Specification

## 1. Skill의 정의

**Skill은 Devian Framework의 확장 단위다.**

Skill은:
- 플러그인이 **아니다**
- 선택적 도구가 **아니다**
- 외부 애드온이 **아니다**

**Skill은 Devian Framework를 구성하는 정식 구성 요소다.**

---

## 2. Skill의 역할

Skill은 다음 중 하나 이상을 수행한다:

| 역할 | 설명 |
|------|------|
| 코드 생성 | 특정 런타임을 위한 코드 생성 |
| 데이터 변환 | 특정 플랫폼을 위한 데이터 변환 |
| 규약 제공 | 특정 스택(NestJS, Unity, C# 등)에 대한 규약 |
| 런타임 연결 | 빌드 결과를 실제 실행 환경에 연결 |

---

## 3. 공식 Skill 범주

다음은 Devian Framework의 공식 Skill 범주다:

### Server Skills

| Skill | 설명 |
|-------|------|
| NestJS Server Skill | API, 네트워크, 서버 런타임 연결 |
| Express Server Skill | 경량 서버 런타임 |

### Client Skills

| Skill | 설명 |
|-------|------|
| C# Network Client Skill | 메시지/프로토콜 소비 |
| TypeScript Client Skill | 웹 클라이언트 |

### Engine Skills

| Skill | 설명 |
|-------|------|
| Unity Skill | 게임 엔진 런타임 연결 |
| Godot Skill | Godot 엔진 연결 |

### Tooling Skills

| Skill | 설명 |
|-------|------|
| Validation Skill | 데이터 검증 |
| Test Skill | 자동화 테스트 |
| Visualization Skill | 데이터 시각화 |

이 목록은 확장 가능하지만, **모든 확장은 반드시 Skill로 정의된다.**

---

## 4. Skill 설계 원칙

| # | 원칙 |
|---|------|
| 1 | Skill은 Framework 규약을 **변경하지 않는다** |
| 2 | Skill은 정의 포맷을 **독점하지 않는다** |
| 3 | Skill은 단일 빌드 흐름에 **종속된다** |
| 4 | Skill은 독립적으로 **추가·제거 가능**해야 한다 |

---

## 5. Framework vs Skill 경계

| 항목 | Framework | Skill |
|------|:---------:|:-----:|
| 정의 포맷 | ✔ | ✖ |
| 빌드 규약 | ✔ | ✖ |
| 코드 생성 규칙 | ✔ | ✖ |
| 런타임 연결 | ✖ | ✔ |
| 플랫폼 의존성 | ✖ | ✔ |
| 네트워크 스택 | ✖ | ✔ |
| 엔진 로직 | ✖ | ✔ |

---

## 6. Skill 디렉토리 구조

```
skills/
├── devian/              ← Framework 핵심 스킬 (규약)
│   ├── 00-rules-minimal/
│   ├── 01-devian-core-philosophy/
│   ├── 02-skill-specification/
│   └── ...
│
├── server/              ← Server Skills
│   ├── nestjs/
│   └── express/
│
├── client/              ← Client Skills
│   ├── csharp-network/
│   └── typescript/
│
├── engine/              ← Engine Skills
│   ├── unity/
│   └── godot/
│
└── tooling/             ← Tooling Skills
    ├── validation/
    └── test/
```

---

## 7. Skill 문서 구조

모든 Skill 문서는 다음 구조를 따른다:

```markdown
# {Category} – {Skill Name}

## Purpose
## Scope (In/Out)
## Hard Rules (MUST)
## Soft Rules (SHOULD)
## Inputs / Outputs
## Integration Points
## Related Skills
## Version History
```

---

## Hard Rules (MUST)

| # | Rule |
|---|------|
| 1 | 모든 확장은 **Skill**로 정의한다 |
| 2 | Skill은 Framework 규약을 **변경하지 않는다** |
| 3 | Skill은 단일 빌드 흐름에 **종속된다** |
| 4 | NestJS, Unity 관련 기능은 **Skill**이다 |

---

## Soft Rules (SHOULD)

| # | Rule |
|---|------|
| 1 | Skill은 명확한 범주(Server/Client/Engine/Tooling)에 속해야 한다 |
| 2 | Skill 간 의존성은 최소화한다 |
| 3 | Skill은 자체 테스트를 포함해야 한다 |

---

## Related Skills

| Skill | 관계 |
|-------|------|
| `01-devian-core-philosophy` | Framework 철학 |
| `00-rules-minimal` | Hard Rules |
| `60-build-pipeline` | 빌드 규약 |

---

## Version History

| Version | Date | Changes |
|---------|------|---------|
| 1.0.0 | 2024-12-25 | Initial Skill specification |
