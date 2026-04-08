using Devian;
using UnityEngine;

public class UILobbyHeroEquipGridCell : UIScrollGridCell
{
    protected override void onShow(int cellIndex)
    {
        var items = InventoryManager.Instance.EquippedItems;
        if (cellIndex >= items.Count)
        {
            return;
        }
        var item = items[cellIndex];
    }
}
