using UnityEngine;
using Devian;

public class UIGameBagPanel : UIPanel<UIGameCanvas>
{
    protected override void onInit(UIGameCanvas canvas)
    {
        base.onInit(canvas);
        // scrollView는 UIBaseContainer 수명주기로 자동 초기화됨
        // Grid.SetCellCount() + 콜백 설정은 여기서 수행
    }
}
