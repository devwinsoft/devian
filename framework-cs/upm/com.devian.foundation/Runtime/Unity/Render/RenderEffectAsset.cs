using System;
using System.Collections.Generic;
using UnityEngine;

namespace Devian
{
    public abstract class RenderEffectAsset : ScriptableObject
    {
        [SerializeField] private int _priority = 0;
        public int Priority => _priority;

        private readonly Stack<IRenderEffect> _pool = new Stack<IRenderEffect>();

        public IRenderEffect Rent()
        {
            if (_pool.Count > 0)
            {
                return _pool.Pop();
            }

            var created = CreateInstanceInternal();
            if (created == null)
            {
                throw new InvalidOperationException($"[RenderEffectAsset] CreateInstanceInternal returned null: {name}");
            }
            return created;
        }

        public void Return(IRenderEffect effect)
        {
            if (effect == null) return;
            effect.Reset();
            _pool.Push(effect);
        }

        // Asset이 구체 인스턴스를 생성한다.
        protected abstract IRenderEffect CreateInstanceInternal();
    }
}
