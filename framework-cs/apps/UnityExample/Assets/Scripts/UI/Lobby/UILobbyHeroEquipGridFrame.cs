using Devian;
using UnityEngine;

public class UILobbyHeroEquipGridFrame : UIScrollGridFrame<UILobbyHeroEquipGridCell>
{
    protected override void onInit()
    {
        var items = InventoryManager.Instance.EquippedItems;
        SetCellCount(items.Count);
    }
}
