using UnityEngine;
using Devian;
using Devian.Domain.Game;

public class UILobbyHeroEquipUnownedGridFrame : UIScrollGridFrame<UILobbyHeroEquipUnownedGridCell>
{
    protected override void onInit()
    {
        var unowned = InventoryManager.Instance.UnownedEquipItems;
        SetCellCount(unowned.Count);
    }
}
