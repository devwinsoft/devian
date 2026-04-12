using Devian;
using UnityEngine;

public class UILobbyHeroEquipOwnedGridFrame : UIScrollGridFrame<UILobbyHeroEquipOwnedGridCell>
{
    protected override void onInit()
    {
        var equiped = InventoryManager.Instance.EquippedItems;
        var unequipped = InventoryManager.Instance.UnequippedItems;
        SetCellCount(equiped.Count + unequipped.Count);
    }
}
