using UnityEngine;
using UnityEngine.UI;
using System.Threading.Tasks;
using Devian;
using Devian.Domain.Game;
using TMPro;

public abstract class UILobbyShopDailyItemBase : UIBaseFrame
{
    public TextMeshProUGUI itemNameText;
    public Image dim;

    protected override void onInit()
    {
    }
}
