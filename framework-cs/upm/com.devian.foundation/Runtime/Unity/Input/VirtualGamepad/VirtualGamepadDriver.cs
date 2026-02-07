using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace Devian
{
    /// <summary>
    /// VirtualGamepad에 상태를 주입하는 드라이버.
    /// CompoSingleton — 씬에 배치하면 전역 접근 가능, 씬 전환 시 파괴된다.
    /// UI(UIVirtualPad 등)에서 SetMove/SetLook/SetButton을 호출하면,
    /// 매 프레임 InputSystem.QueueStateEvent로 디바이스에 상태를 주입한다.
    /// </summary>
    public sealed class VirtualGamepadDriver : CompoSingleton<VirtualGamepadDriver>
    {
        /// <summary>
        /// 씬 단위 수명 — DontDestroyOnLoad 비활성.
        /// </summary>
        protected override bool DontDestroy => false;

        private VirtualGamepad _device;
        private VirtualGamepadState _state;
        private bool _dirty;

        // ---- Public API ----

        /// <summary>
        /// Move 값을 설정한다. 다음 프레임에 디바이스에 주입된다.
        /// </summary>
        public void SetMove(Vector2 value)
        {
            _state.move = value;
            _dirty = true;
        }

        /// <summary>
        /// Look 값을 설정한다. 다음 프레임에 디바이스에 주입된다.
        /// </summary>
        public void SetLook(Vector2 value)
        {
            _state.look = value;
            _dirty = true;
        }

        /// <summary>
        /// 버튼 비트를 설정한다. 다음 프레임에 디바이스에 주입된다.
        /// </summary>
        public void SetButton(uint bits)
        {
            _state.buttons = bits;
            _dirty = true;
        }

        // ---- Lifecycle ----

        protected override void Awake()
        {
            base.Awake();

            _device = VirtualGamepadUtility.CreateDevice();
            _state = default;
            _dirty = false;
        }

        private void Update()
        {
            if (_device == null || !_device.added) return;

            if (_dirty)
            {
                InputSystem.QueueStateEvent(_device, _state);
                _dirty = false;
            }
        }

        protected override void OnDestroy()
        {
            VirtualGamepadUtility.RemoveDevice(_device);
            _device = null;

            base.OnDestroy();
        }
    }
}
