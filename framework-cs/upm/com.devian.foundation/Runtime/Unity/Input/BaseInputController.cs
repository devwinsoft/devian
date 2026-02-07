using System.Collections.Generic;
using UnityEngine;

namespace Devian
{
    /// <summary>
    /// BaseController 기반 입력 소비 컨트롤러.
    /// onInit에서 InputManager.RegisterController, Clear에서 UnregisterController.
    /// InputEnabled == false이면 콜백 무시.
    /// 변화가 있을 때만 4개 virtual 콜백을 호출한다.
    /// </summary>
    public class BaseInputController : BaseController, IBaseInputController
    {
        [SerializeField] private float _axisEpsilon = 0.001f;

        private bool _hasPrev;
        private Vector2 _prevMove;
        private Vector2 _prevLook;
        private ulong _prevButtons;

        public bool InputEnabled { get; set; } = true;

        public IInputSpace InputSpace { get; set; }

        // ---- Actor lifecycle ----

        protected override void onInit(BaseActor actor)
        {
            InputManager.Instance.RegisterController(this);

            _hasPrev = false;
            _prevMove = default;
            _prevLook = default;
            _prevButtons = 0UL;
        }

        public override void Clear()
        {
            // 에디터 종료/플레이 종료/씬 종료 정리 중이면 매니저 접근 금지
            if (BaseBootstrap.IsShuttingDown)
            {
                base.Clear();
                return;
            }

            if (!IsCleared && SingletonRegistry.TryGet<InputManager>(out var mgr))
                mgr.UnregisterController(this);

            _hasPrev = false;
            _prevMove = default;
            _prevLook = default;
            _prevButtons = 0UL;

            base.Clear();
        }

        // ---- Virtual callbacks (override in subclass) ----

        /// <summary>
        /// Move 값이 변화했을 때 호출된다.
        /// </summary>
        protected virtual void onInputMove(Vector2 move) { }

        /// <summary>
        /// Look 값이 변화했을 때 호출된다.
        /// </summary>
        protected virtual void onInputLook(Vector2 look) { }

        /// <summary>
        /// 버튼이 눌렸을 때 호출된다.
        /// </summary>
        protected virtual void onButtonPress(string key, int index) { }

        /// <summary>
        /// 버튼이 떼어졌을 때 호출된다.
        /// </summary>
        protected virtual void onButtonRelease(string key, int index) { }

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
                onInputMove(frame.Move);

            // Look
            if (!_hasPrev || (frame.Look - _prevLook).sqrMagnitude > eps2)
                onInputLook(frame.Look);

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
                    onButtonPress(key, i);
                }

                if ((upMask & bit) != 0UL)
                {
                    string key = _keyOrEmpty(keys, i);
                    onButtonRelease(key, i);
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
