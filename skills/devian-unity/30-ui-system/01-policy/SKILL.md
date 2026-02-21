# 30-ui-system Policy

Status: ACTIVE
AppliesTo: v10

## Purpose

`skills/devian-unity/30-ui-system/` 영역 문서(스킬) 작성 규칙을 정의한다.

## Policies

### No "Usage" Section

이 영역의 모든 스킬 문서에서 `Usage` 또는 `Usage Examples` 섹션을 만들지 않는다.

- 금지 예:
  - `## Usage`
  - `## Usage Examples`
  - 유사 의미의 "사용법/사용 예제" 단독 섹션

#### Rationale
- 스킬 문서가 "정본 규약/SSOT/정책" 중심으로 유지되어야 하며,
  사용 예시는 코드/테스트/샘플 프로젝트 또는 상위 문서(overview)로 분리하는 것이 유지보수에 유리하다.

#### Allowed Alternatives
- 필요한 경우:
  - `## Notes` 또는 `## Reference` 하위에 "짧은 주의사항" 수준으로만 기술
  - 실제 예제 코드는 SSOT의 Code Path(정본 코드) 또는 테스트/샘플로 이동
