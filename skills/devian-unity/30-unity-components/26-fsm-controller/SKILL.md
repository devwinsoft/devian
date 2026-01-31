# FSM Controller

## 0. 목적
실전적으로 빠르고 안전한 상태 머신(FSM)을 제공한다.
전이는 try 없이 "무조건 성공"을 전제로 하며, onEnter/onExit 로직이 중요하므로 전이를 건너뛰지 않는다.

---

## 1. 구성
- BaseFsmObject<TState, TOwner>
- BaseFsmController<TState, TFsm, TOwner>

---

## 2. 규약 (Hard)
- namespace는 Devian 고정.
- 전이는 FIFO 큐로 처리한다.
- 미등록 state는 error이며 ChangeState 호출 시 즉시 예외(throw).
- 큐에 enqueue된 전이는 절대 스킵/덮어쓰기/합치기 없이 모두 실행한다.
- self-transition 허용 여부는 상태의 AllowSelfTransition만으로 결정된다.
- 런타임에서 self-transition 정책을 바꾸는 기능은 제공하지 않는다.
- context/owner는 전이마다 new로 만들지 않고, 컨트롤러 Init(owner)로 1회 바인딩 후 재사용한다.
- 무한 전이 방지를 위해 1 flush당 최대 전이 수 제한이 있으며 초과 시 예외(throw).
- 프레임워크 FSM에 Cancel() 기능은 제공하지 않는다. "상태 종료/취소/롤백/중단"은 게임/비즈니스 로직에서 별도 상태/전이로 구현한다.

---

## 3. API 요약
- Register(state)
- Start(initialState, args...)
- ChangeState(state, args...)
- Tick / FixedTick / LateTick
