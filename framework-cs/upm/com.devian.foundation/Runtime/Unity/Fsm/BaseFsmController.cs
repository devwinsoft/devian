using System;
using System.Collections.Generic;

namespace Devian
{
    /// <summary>
    /// FIFO transition FSM controller. No try-guard: missing state is an error (throws).
    /// All queued transitions are executed in order (no skipping).
    /// </summary>
    public abstract class BaseFsmController<TState, TFsm, TOwner>
        where TState : struct, IConvertible
        where TFsm : BaseFsmObject<TState, TOwner>
    {
        private struct Transition
        {
            public TState Dest;
            public object[] Args;
        }

        private const int MaxTransitionsPerFlush = 256;

        protected TOwner Owner { get; private set; }

        protected TFsm CurrentStateObject => mCurrentState;
        public TState CurrentStateType => mCurrentState != null ? mCurrentState.State : default;

        private readonly Dictionary<TState, TFsm> mStates = new();
        private readonly Queue<Transition> mQueue = new();

        private TFsm mCurrentState;
        private bool mIsChanging;

        public void Init(TOwner owner)
        {
            Owner = owner;
            OnInit();
        }

        protected virtual void OnInit() { }

        public void Register(TFsm fsm)
        {
            if (fsm == null) throw new ArgumentNullException(nameof(fsm));

            mStates[fsm.State] = fsm;
            fsm.Init(Owner);
        }

        public void Start(TState initialState, params object[] args)
        {
            ChangeState(initialState, args);
        }

        public void ChangeState(TState state, params object[] args)
        {
            _EnqueueChange(state, args);
            _ProcessQueue();
        }

        public void Tick(float dt)
        {
            mCurrentState?.Tick(dt);
        }

        public void FixedTick(float dt)
        {
            mCurrentState?.FixedTick(dt);
        }

        public void LateTick(float dt)
        {
            mCurrentState?.LateTick(dt);
        }

        protected virtual void OnStateChanged(TState from, TState to) { }

        private void _EnqueueChange(TState state, object[] args)
        {
            if (!mStates.ContainsKey(state))
                throw new InvalidOperationException($"Cannot find FSM state: {state}");

            if (mCurrentState != null && state.Equals(mCurrentState.State))
            {
                if (!mCurrentState.AllowSelfTransition)
                    return;
            }

            mQueue.Enqueue(new Transition
            {
                Dest = state,
                Args = args
            });
        }

        private void _ProcessQueue()
        {
            if (mIsChanging) return;
            mIsChanging = true;

            var processed = 0;

            while (mQueue.Count > 0)
            {
                processed++;
                if (processed > MaxTransitionsPerFlush)
                {
                    throw new InvalidOperationException(
                        $"FSM transition overflow: >{MaxTransitionsPerFlush} transitions in one flush. " +
                        $"Possible infinite transition loop.");
                }

                var t = mQueue.Dequeue();
                var from = CurrentStateType;

                // state existence already validated at enqueue, but keep as hard safety
                if (!mStates.TryGetValue(t.Dest, out var next) || next == null)
                {
                    throw new InvalidOperationException($"Cannot find FSM state during commit: {t.Dest}");
                }

                mCurrentState?.Exit(t.Dest);
                mCurrentState = next;
                mCurrentState.Enter(from, t.Args);

                OnStateChanged(from, t.Dest);
            }

            mIsChanging = false;
        }
    }
}
