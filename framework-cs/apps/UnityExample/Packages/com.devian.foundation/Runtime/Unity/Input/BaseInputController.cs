using System.Collections.Generic;
using UnityEngine;

namespace Devian
{
    /// <summary>
    /// MonoBehaviour 기반 입력 소비 컨트롤러.
    /// OnEnable에서 InputManager.RegisterController, OnDisable에서 UnregisterController.
    /// InputEnabled == false이면 콜백 무시.
    /// 변화가 있을 때만 4개 virtual 콜백을 호출한다.
    /// </summary>
    public abstract class BaseInputController : MonoBehaviour, IBaseInputController
    {
        [SerializeField] private float _axisEpsilon = 0.001f;

        private bool _hasPrev;
        private Vector2 _prevMove;
        private Vector2 _prevLook;
        private ulong _prevButtons;

        public bool InputEnabled { get; set; } = true;

        public virtual int Priority => 0;

        public IInputSpace InputSpace { get; set; }

        // ---- Lifecycle ----

        protected virtual void OnEnable()
        {
            InputManager.Instance.RegisterController(this);
        }

        protected virtual void OnDisable()
        {
            InputManager.Instance.UnregisterController(this);

            _hasPrev = false;
            _prevMove = default;
            _prevLook = default;
            _prevButtons = 0UL;
        }

        // ---- Virtual callbacks (override in subclass) ----

        /// <summary>
        /// Move 값이 변화했을 때 호출된다.
        /// </summary>
        protected virtual void OnInputMove(Vector2 move) { }

        /// <summary>
        /// Look 값이 변화했을 때 호출된다.
        /// </summary>
        protected virtual void OnInputLook(Vector2 look) { }

        /// <summary>
        /// 버튼이 눌렸을 때 호출된다.
        /// </summary>
        protected virtual void OnButtonPress(string key, int index) { }

        /// <summary>
        /// 버튼이 떼어졌을 때 호출된다.
        /// </summary>
        protected virtual void OnButtonRelease(string key, int index) { }

        // ---- Internal entry point (called by InputManager) ----

        /// <summary>
        /// InputManager가 호출하는 엔트리 포인트.
        /// 외부 코드에서 직접 호출하지 않는다.
        /// </summary>
        public void __Consume(InputFrame frame)
        {
            _onInputFrame(frame);
        }

        // ---- Private ----

        private void _onInputFrame(InputFrame frame)
        {
            if (!InputEnabled) return;

            float eps2 = _axisEpsilon * _axisEpsilon;

            // Move
            if (!_hasPrev || (frame.Move - _prevMove).sqrMagnitude > eps2)
                OnInputMove(frame.Move);

            // Look
            if (!_hasPrev || (frame.Look - _prevLook).sqrMagnitude > eps2)
                OnInputLook(frame.Look);

            // Buttons
            ulong cur = frame.ButtonBits;
            ulong prev = _hasPrev ? _prevButtons : 0UL;

            ulong down = cur & ~prev;
            ulong up = prev & ~cur;

            if (down != 0UL || up != 0UL)
                _dispatchButtons(down, up);

            // save prev
            _hasPrev = true;
            _prevMove = frame.Move;
            _prevLook = frame.Look;
            _prevButtons = cur;
        }

        private void _dispatchButtons(ulong downMask, ulong upMask)
        {
            var keys = InputManager.Instance.ButtonKeys;

            for (int i = 0; i < 64; i++)
            {
                ulong bit = 1UL << i;

                if ((downMask & bit) != 0UL)
                {
                    string key = _keyOrEmpty(keys, i);
                    OnButtonPress(key, i);
                }

                if ((upMask & bit) != 0UL)
                {
                    string key = _keyOrEmpty(keys, i);
                    OnButtonRelease(key, i);
                }
            }
        }

        private static string _keyOrEmpty(IReadOnlyList<string> keys, int index)
        {
            if (keys == null) return string.Empty;
            if ((uint)index >= (uint)keys.Count) return string.Empty;
            return keys[index] ?? string.Empty;
        }
    }
}
