using UnityEngine;

namespace Devian
{
    /// <summary>
    /// UI Sample: UIVirtualPad -> VirtualGamepadDriver(move) 브릿지.
    /// UIVirtualPad의 CurrentValue를 매 프레임 VirtualGamepadDriver.SetMove로 전달한다.
    /// InputManager는 수정하지 않고, InputSystem 가상 디바이스로만 주입한다.
    /// </summary>
    public sealed class UIVirtualPadToVirtualGamepadBridge : MonoBehaviour
    {
        [SerializeField] private UIVirtualPad _pad;

        private void Reset()
        {
            _pad = GetComponent<UIVirtualPad>();
        }

        private void Update()
        {
            if (_pad == null) return;
            if (!VirtualGamepadDriver.TryGet(out var driver)) return;

            driver.SetMove(_pad.CurrentValue);
        }

        private void OnDisable()
        {
            if (!VirtualGamepadDriver.TryGet(out var driver)) return;
            driver.SetMove(Vector2.zero);
        }
    }
}
