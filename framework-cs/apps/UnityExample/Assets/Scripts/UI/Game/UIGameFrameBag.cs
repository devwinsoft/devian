using UnityEngine;
using Devian;

public class UIGameFrameBag : UIFrame<UIGameCanvas>
{
    public UIComponentScrollContainer scrollContainer;

    protected override void onInit(UIGameCanvas canvas)
    {
        base.onInit(canvas);
        // scrollContainer는 UIComponentBase 수명주기로 자동 초기화됨
        // Grid.SetCellCount() + 콜백 설정은 여기서 수행
    }
}
