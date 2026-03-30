using UnityEngine;
using Devian;
using Devian.Domain.Game;
using TMPro;

public class UILobbyMenuTopPanel : UIBasePanel<UILobbyCanvas>
{
    public TextMeshProUGUI stamina;
    public TextMeshProUGUI jewel;
    
    protected override void onInit(UILobbyCanvas canvas)
    {
        stamina.text = string.Format("Stamina: {0}/{1}",
            InventoryManager.Instance.Storage.Wallet.Get(CURRENCY_TYPE.STAMINA),
            InventoryManager.Instance.MaxStamina);
        jewel.text = string.Format("Jewel: {0}",
            InventoryManager.Instance.Storage.Wallet.Get(CURRENCY_TYPE.JEWEL));
    }
}
