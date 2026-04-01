using UnityEngine;
using System.Threading;
using System.Threading.Tasks;
using Devian;
using Devian.Domain.Common;
using Devian.Domain.Game;

public class UILobbySettingPanel : UIBasePanel<UILobbyPageCanvas>
{
    protected override void onInit(UILobbyPageCanvas pageCanvas)
    {
    }
    
    public void OnClick_DVN_Import()
    {
        UnityTaskRunner.Run(RecoveryManager.Instance.PickAndImportDvnAsync(CancellationToken.None), "OnClick_DVN_Import");
    }

    public void OnClick_DVN_Export()
    {
        UnityTaskRunner.Run(RecoveryManager.Instance.ExportDvnViaEmailAsync("maoshy@gmail.com", CancellationToken.None), "OnClick_DVN_Export");
    }
}
