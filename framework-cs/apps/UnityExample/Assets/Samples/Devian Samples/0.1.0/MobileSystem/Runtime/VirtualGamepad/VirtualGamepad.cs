using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.Layouts;

namespace Devian
{
    [InputControlLayout(displayName = "Virtual Gamepad", stateType = typeof(VirtualGamepadState))]
    public sealed class VirtualGamepad : InputDevice
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
