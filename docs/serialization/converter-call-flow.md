# Devian Converter Call Flow (정본)

본 문서는 Devian에서 `IEntityConverter<TEntity>`가 수행하는 직렬화 호출 흐름의 확정 정본이다.
이 문서의 목적은 구현 일관성·재현성·검증 가능성 확보이며, 대안은 허용하지 않는다.

> **범위 명확화:**
> - 이 문서는 `IEntityConverter<TEntity>`의 **Binary/ProtoJSON 호출 흐름 정본**이다.
> - **테이블 정식 JSON I/O는 `28-json-row-io`(일반 JSON NDJSON)**이며, 이 문서의 `ToJson/FromJson` 경로를 사용하지 않는다.
> - `_LoadJson/_SaveJson`도 **ProtoJSON 디버그 전용**이며, 테이블 정식 I/O에서 사용하지 않는다.

---

## 1. 책임 분리 (Responsibility Split)

### Entity (도메인 엔티티)

- 도메인 의미 변환의 최종 책임
- 필드 단위 encode / decode (암호화·복호화 포함)
- 내부 전용 메소드만 제공

```
_LoadProto(TProto msg)
_SaveProto(): TProto
_LoadJson(string json)
_SaveJson(): string
```

### Converter (IEntityConverter<TEntity>)

- 직렬화 파이프라인의 오케스트레이터
- 포맷 처리만 담당
- 엔티티 내부 메소드 호출만 수행

### Parser (protobuf)

- `MessageParser<TProto>`
- Binary → TProto 파싱 전담
- Converter에 의해 주입되어 사용됨

---

## 2. 호출 흐름 정본

### 2.1 FromBinary(byte[] bytes)

**Input**

- protobuf binary payload (`byte[]`)

**Steps**

1. `MessageParser<TProto>.ParseFrom(bytes)` 호출
2. `new TEntity()` 생성
3. `entity._LoadProto(proto)` 호출

**Output**

- 초기화 완료된 `TEntity`

**Failure Conditions**

1. protobuf 파싱 실패 (invalid wire format)
2. `_LoadProto` 내부 decode 실패

---

### 2.2 ToBinary(TEntity entity)

**Input**

- 초기화된 `TEntity`

**Steps**

1. `entity._SaveProto()` 호출
2. 반환된 `TProto`에 대해 `ToByteArray()` 호출

**Output**

- protobuf binary payload (`byte[]`)

**Failure Conditions**

1. `_SaveProto` 내부 encode 실패
2. 필수 필드 누락으로 인한 protobuf 오류

---

### 2.3 FromJson(string json)

**Input**

- protobuf JSON string

**Steps**

1. `JsonParser`로 `TProto` 생성
2. `new TEntity()` 생성
3. `entity._LoadProto(proto)` 호출

**Output**

- 초기화 완료된 `TEntity`

**Failure Conditions**

1. JSON 파싱 실패
2. protobuf JSON 규약 위반
3. `_LoadProto` 내부 decode 실패

---

### 2.4 ToJson(TEntity entity)

**Input**

- 초기화된 `TEntity`

**Steps**

1. `entity._SaveProto()` 호출
2. `JsonFormatter`로 JSON 문자열 생성

**Output**

- protobuf JSON string

**Failure Conditions**

1. `_SaveProto` 내부 encode 실패
2. JSON 직렬화 실패

---

## 3. 강제 규칙 (Mandatory Rules)

- Converter는 엔티티의 `_LoadProto/_SaveProto/_LoadJson/_SaveJson`만 호출한다.
- Converter는 엔티티 필드를 직접 접근하지 않는다.
- JSON 처리는 `Google.Protobuf.JsonParser / JsonFormatter`만 사용한다.
- Binary 파싱은 `MessageParser<TProto>`를 사용하며 `new TProto()`를 사용하지 않는다.
- 내부 전용 메소드는 `_MethodName` 네이밍 규칙을 따른다.

---

## 4. 의사코드 (C#)

```csharp
public TEntity FromBinary(byte[] bytes)
{
    var proto = _parser.ParseFrom(bytes);
    var entity = new TEntity();
    entity._LoadProto(proto);
    return entity;
}

public byte[] ToBinary(TEntity entity)
{
    var proto = entity._SaveProto();
    return proto.ToByteArray();
}

public TEntity FromJson(string json)
{
    var proto = JsonParser.Default.Parse<TProto>(json);
    var entity = new TEntity();
    entity._LoadProto(proto);
    return entity;
}

public string ToJson(TEntity entity)
{
    var proto = entity._SaveProto();
    return JsonFormatter.Default.Format(proto);
}
```

---

## 5. 본 문서의 지위

- 본 문서는 정본이며, 구현은 반드시 이 흐름을 따른다.
- 성능·대안·리팩토링 논의는 별도 문서에서만 수행한다.

---

## Version History

| Version | Date | Changes |
|---------|------|---------|
| 1.1.0 | 2024-12-28 | **범위 명확화**: 테이블 NDJSON(28)과 무관함 명시, ProtoJSON 디버그 전용 강조 |
| 1.0.0 | 2024-12-27 | 정본 작성 |
