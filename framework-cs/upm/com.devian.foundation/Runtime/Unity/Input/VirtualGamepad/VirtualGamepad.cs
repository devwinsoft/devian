using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.Layouts;

namespace Devian
{
    /// <summary>
    /// InputSystem에 등록되는 커스텀 가상 게임패드 디바이스.
    /// 바인딩 예: &lt;VirtualGamepad&gt;/move, &lt;VirtualGamepad&gt;/look, &lt;VirtualGamepad&gt;/dash
    /// </summary>
    [InputControlLayout(stateType = typeof(VirtualGamepadState), displayName = "Virtual Gamepad")]
    public class VirtualGamepad : InputDevice
    {
        public StickControl move { get; private set; }
        public StickControl look { get; private set; }
        public ButtonControl dash { get; private set; }

        protected override void FinishSetup()
        {
            base.FinishSetup();

            move = GetChildControl<StickControl>("move");
            look = GetChildControl<StickControl>("look");
            dash = GetChildControl<ButtonControl>("dash");
        }
    }
}
