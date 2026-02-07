using UnityEngine;
using UnityEngine.InputSystem.Layouts;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.Utilities;

namespace Devian
{
    /// <summary>
    /// 가상 게임패드의 InputSystem 상태 구조체.
    /// move(Vector2), look(Vector2), dash(button) 등을 담는다.
    /// </summary>
    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Explicit, Size = 20)]
    public struct VirtualGamepadState : IInputStateTypeInfo
    {
        public FourCC format => new FourCC('V', 'G', 'P', 'D');

        [InputControl(layout = "Stick", usage = "Move")]
        [System.Runtime.InteropServices.FieldOffset(0)]
        public Vector2 move;

        [InputControl(layout = "Stick", usage = "Look")]
        [System.Runtime.InteropServices.FieldOffset(8)]
        public Vector2 look;

        [InputControl(layout = "Button", name = "dash", usage = "Dash", bit = 0)]
        [System.Runtime.InteropServices.FieldOffset(16)]
        public uint buttons;

        /// <summary>
        /// 편의 생성. move/look만 지정.
        /// </summary>
        public static VirtualGamepadState Create(Vector2 move, Vector2 look, uint buttons = 0)
        {
            return new VirtualGamepadState
            {
                move = move,
                look = look,
                buttons = buttons,
            };
        }
    }
}
