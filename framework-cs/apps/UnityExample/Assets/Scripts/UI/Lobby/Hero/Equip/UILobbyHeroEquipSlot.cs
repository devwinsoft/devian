using UnityEngine;
using Devian;
using Devian.Domain.Game;

public class UILobbyHeroEquipSlot : UIComponentBase
{
    public EQUIP_SLOT_TYPE slotType;

    protected override void onInit(Canvas canvas)
    {
        if (InventoryManager.Instance.SelectedHero.Equips.TryGetValue(slotType, out var equip))
        {
            Debug.Log($"Equip: {equip.ItemId}");
        }
        else
        {
        }
    }
}
