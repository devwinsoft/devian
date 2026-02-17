using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Layouts;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.Utilities;

namespace Devian
{
    [StructLayout(LayoutKind.Sequential)]
    public struct VirtualGamepadState : IInputStateTypeInfo
    {
        public FourCC format => new FourCC('V', 'G', 'P', 'D');

        [InputControl(layout = "Stick")]
        public Vector2 move;

        [InputControl(layout = "Stick")]
        public Vector2 look;

        // Buttons (bitfield)
        [InputControl(name = "dash", layout = "Button", bit = 0)]
        public uint buttons;

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
