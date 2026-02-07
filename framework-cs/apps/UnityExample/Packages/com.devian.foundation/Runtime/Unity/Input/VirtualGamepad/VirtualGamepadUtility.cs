using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Layouts;

namespace Devian
{
    /// <summary>
    /// VirtualGamepad 레이아웃 등록 / 디바이스 생성·제거 유틸.
    /// </summary>
    public static class VirtualGamepadUtility
    {
        private static bool _layoutRegistered;

        /// <summary>
        /// VirtualGamepad 레이아웃을 InputSystem에 등록한다.
        /// 중복 호출은 무시된다.
        /// </summary>
        public static void EnsureLayout()
        {
            if (_layoutRegistered) return;

            InputSystem.RegisterLayout<VirtualGamepad>();
            _layoutRegistered = true;
        }

        /// <summary>
        /// VirtualGamepad 디바이스를 생성하여 InputSystem에 추가한다.
        /// 레이아웃이 아직 등록되지 않았다면 자동 등록한다.
        /// </summary>
        public static VirtualGamepad CreateDevice()
        {
            EnsureLayout();
            return InputSystem.AddDevice<VirtualGamepad>();
        }

        /// <summary>
        /// VirtualGamepad 디바이스를 InputSystem에서 제거한다.
        /// </summary>
        public static void RemoveDevice(VirtualGamepad device)
        {
            if (device == null) return;
            if (device.added)
            {
                InputSystem.RemoveDevice(device);
            }
        }

        /// <summary>
        /// 현재 등록된 VirtualGamepad 디바이스를 반환한다. 없으면 null.
        /// </summary>
        public static VirtualGamepad GetDevice()
        {
            return InputSystem.GetDevice<VirtualGamepad>();
        }
    }
}
