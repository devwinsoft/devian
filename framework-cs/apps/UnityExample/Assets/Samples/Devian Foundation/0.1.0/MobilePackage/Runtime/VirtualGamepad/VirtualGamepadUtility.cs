using UnityEngine.InputSystem;

namespace Devian
{
    public static class VirtualGamepadUtility
    {
        private const string LayoutName = "VirtualGamepad";

        public static void EnsureLayout()
        {
            if (InputSystem.LoadLayout(LayoutName) != null)
            {
                return;
            }

            InputSystem.RegisterLayout<VirtualGamepad>(LayoutName);
        }

        public static VirtualGamepad CreateDevice()
        {
            EnsureLayout();

            var existing = GetDevice();
            if (existing != null)
            {
                return existing;
            }

            return InputSystem.AddDevice<VirtualGamepad>(LayoutName);
        }

        public static void RemoveDevice(VirtualGamepad device)
        {
            if (device == null)
            {
                return;
            }

            InputSystem.RemoveDevice(device);
        }

        public static VirtualGamepad GetDevice()
        {
            return InputSystem.GetDevice<VirtualGamepad>();
        }
    }
}
