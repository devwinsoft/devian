using UnityEngine;
using Devian;
using Devian.Domain.Game;
using TMPro;

public class UILobbyMenuTopPanel : UIBasePanel<UILobbyPageCanvas>
{
    public TextMeshProUGUI stamina;
    public TextMeshProUGUI gold;
    public TextMeshProUGUI jewel;

    protected override void onInit(UILobbyPageCanvas pageCanvas)
    {
        refreshWalletTexts();

        var inventoryManager = InventoryManager.Instance;
        inventoryManager.Subcribe(GetEntityId(), INVENTORY_MESSAGE_TYPE.CURRENCY_CHANGED,
            (args) =>
            {
                refreshWalletTexts();
                return false;
            });

        inventoryManager.Subcribe(GetEntityId(), INVENTORY_MESSAGE_TYPE.INVENTORY_SNAPSHOT_CHANGED,
            (args) =>
            {
                refreshWalletTexts();
                return false;
            });
    }

    void refreshWalletTexts()
    {
        stamina.text = string.Format("Stamina: {0}/{1}",
            InventoryManager.Instance.GetCurrencyAmount(CURRENCY_TYPE.STAMINA),
            InventoryManager.Instance.MaxStamina);
        gold.text = string.Format("Gold: {0}", InventoryManager.Instance.GetCurrencyAmount(CURRENCY_TYPE.GOLD));
        jewel.text = string.Format("Jewel: {0}",
            InventoryManager.Instance.GetCurrencyAmount(CURRENCY_TYPE.JEWEL));
    }
}
