using System;

namespace Devian
{
    /// <summary>
    /// Base state object. Owner/context is bound once and reused.
    /// </summary>
    public abstract class BaseFsmObject<TState, TOwner>
        where TState : struct, IConvertible
    {
        public abstract TState State { get; }

        protected TOwner Owner { get; private set; }

        /// <summary>
        /// Default policy: self-transition is not allowed.
        /// Override to true if Exit/Enter must run even when re-entering same state.
        /// </summary>
        public virtual bool AllowSelfTransition => false;

        public void Init(TOwner owner)
        {
            Owner = owner;
            OnInit();
        }

        protected virtual void OnInit() { }

        public virtual void Enter(TState from, object[] args) { }

        public virtual void Exit(TState to) { }

        public virtual void Tick(float dt) { }

        public virtual void FixedTick(float dt) { }

        public virtual void LateTick(float dt) { }
    }
}
