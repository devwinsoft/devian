#nullable enable
using UnityEngine;

namespace Devian
{
    /// <summary>
    /// Unity MonoBehaviour that hosts a NetTickLoop and ticks all registered INetTickable objects.
    ///
    /// This is the standard way to manage network client updates in Unity:
    /// 1. Create or get a NetTickRunner (singleton pattern recommended)
    /// 2. Register your INetTickable clients (e.g., NetWsClient)
    /// 3. The runner will call Tick() on all registered clients each frame
    ///
    /// This component is protocol-agnostic - it only depends on Core types (INetTickable).
    /// </summary>
    public sealed class NetTickRunner : MonoBehaviour
    {
        /// <summary>
        /// The underlying tick loop that manages registered tickables.
        /// </summary>
        public NetTickLoop Loop { get; } = new NetTickLoop();

        [SerializeField]
        [Tooltip("If true, this GameObject will persist across scene loads.")]
        private bool _dontDestroyOnLoad = true;

        private void Awake()
        {
            if (_dontDestroyOnLoad)
                DontDestroyOnLoad(gameObject);
        }

        /// <summary>
        /// Register an INetTickable to be ticked each frame.
        /// </summary>
        /// <param name="tickable">The tickable to register.</param>
        public void Register(INetTickable tickable) => Loop.Register(tickable);

        /// <summary>
        /// Unregister an INetTickable.
        /// </summary>
        /// <param name="tickable">The tickable to unregister.</param>
        public void Unregister(INetTickable tickable) => Loop.Unregister(tickable);

        private void Update()
        {
            Loop.TickAll();
        }
    }
}
