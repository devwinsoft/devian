#nullable enable
using System.Collections.Generic;

namespace Devian
{
    /// <summary>
    /// Manages a collection of INetTickable objects and ticks them all together.
    /// This allows multiple network clients to be updated with a single call.
    /// </summary>
    public sealed class NetTickLoop
    {
        private readonly List<INetTickable> _items = new();

        /// <summary>
        /// Register an INetTickable to be ticked. Duplicate registrations are ignored.
        /// </summary>
        /// <param name="item">The tickable to register.</param>
        public void Register(INetTickable item)
        {
            if (item == null) return;
            if (_items.Contains(item)) return;
            _items.Add(item);
        }

        /// <summary>
        /// Unregister an INetTickable. No-op if not registered.
        /// </summary>
        /// <param name="item">The tickable to unregister.</param>
        public void Unregister(INetTickable item)
        {
            if (item == null) return;
            _items.Remove(item);
        }

        /// <summary>
        /// Tick all registered INetTickable objects.
        /// Call this once per frame from the main thread.
        /// </summary>
        public void TickAll()
        {
            for (int i = 0; i < _items.Count; i++)
            {
                _items[i].Tick();
            }
        }

        /// <summary>
        /// Returns the number of registered tickables.
        /// </summary>
        public int Count => _items.Count;
    }
}
