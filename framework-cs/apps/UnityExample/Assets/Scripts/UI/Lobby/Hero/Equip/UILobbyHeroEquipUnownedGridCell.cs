using UnityEngine;
using Devian;
using Devian.Domain.Game;

public class UILobbyHeroEquipUnownedGridCell : UIScrollGridCell
{
    protected override void onShow(int cellIndex)
    {
        var items = InventoryManager.Instance.UnownedEquipItems;
        if (cellIndex >= items.Count)
        {
            return;
        }
        var item = items[cellIndex];
    }
}
