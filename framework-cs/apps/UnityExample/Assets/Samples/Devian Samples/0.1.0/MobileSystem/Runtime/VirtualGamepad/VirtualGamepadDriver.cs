using UnityEngine;
using UnityEngine.InputSystem;

namespace Devian
{
    public sealed class VirtualGamepadDriver : CompoSingleton<VirtualGamepadDriver>
    {
        protected override bool DontDestroy => false;

        private VirtualGamepad _device;
        private VirtualGamepadState _state;
        private bool _dirty;

        protected override void Awake()
        {
            base.Awake();

            _device = VirtualGamepadUtility.CreateDevice();
            _state = VirtualGamepadState.Create(Vector2.zero, Vector2.zero, 0);
            _dirty = true;
        }

        protected override void OnDestroy()
        {
            if (_device != null)
            {
                VirtualGamepadUtility.RemoveDevice(_device);
                _device = null;
            }

            base.OnDestroy();
        }

        public void SetMove(Vector2 value)
        {
            if (_state.move == value)
            {
                return;
            }

            _state.move = value;
            _dirty = true;
        }

        public void SetLook(Vector2 value)
        {
            if (_state.look == value)
            {
                return;
            }

            _state.look = value;
            _dirty = true;
        }

        public void SetButton(uint bits)
        {
            if (_state.buttons == bits)
            {
                return;
            }

            _state.buttons = bits;
            _dirty = true;
        }

        private void Update()
        {
            if (!_dirty || _device == null)
            {
                return;
            }

            InputSystem.QueueStateEvent(_device, _state);
            _dirty = false;
        }
    }
}
