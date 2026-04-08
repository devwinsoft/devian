using UnityEngine;
using Devian;
using Devian.Domain.Game;

public class UILobbyHeroUnownedGridFrame : UIScrollGridFrame<UILobbyHeroUnownedGridCell>
{
    protected override void onInit()
    {
        var unowned = InventoryManager.Instance.UnownedEquipItems;
        SetCellCount(unowned.Count);
    }
}
