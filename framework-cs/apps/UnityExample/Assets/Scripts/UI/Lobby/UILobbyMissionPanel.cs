using UnityEngine;
using Devian;
using Devian.Domain.Common;
using Devian.Domain.Game;

public class UILobbyMissionPanel : UIBasePanel<UILobbyCanvas>
{
    protected override void onAwake()
    {
    }
    
    protected override void onDestroy()
    {
        AchieveManager.Instance?.UnSubcribe(GetEntityId());
        MissionManager.Instance?.UnSubcribe(GetEntityId());
    }
    
    protected override void onInit(UILobbyCanvas canvas)
    {
        AchieveManager.Instance.Subcribe(GetEntityId(),
            ACHIEVE_MESSAGE_TYPE.RUNTIME_INIT,
            (args) =>
            {
                AchieveRuntimeBase achieve = args[0] as AchieveRuntimeBase;
                Debug.Log($"Init: achieveId={achieve.achieveId}, progressValue={achieve.progressValue}");
                return false;
            });

        AchieveManager.Instance.Subcribe(GetEntityId(),
            ACHIEVE_MESSAGE_TYPE.RUNTIME_PROGRESS,
            (args) =>
            {
                AchieveRuntimeBase achieve = args[0] as AchieveRuntimeBase;
                Debug.Log($"Progress: achieveId={achieve.achieveId}, progressValue={achieve.progressValue}");
                return false;
            });

        AchieveManager.Instance.Subcribe(GetEntityId(),
            ACHIEVE_MESSAGE_TYPE.RUNTIME_REWARDED,
            (args) =>
            {
                AchieveRuntimeBase achieve = args[0] as AchieveRuntimeBase;
                RewardData[] rewards = args[1] as RewardData[];
                foreach (var rewward in rewards)
                {
                    Debug.Log($"type={rewward.Type}, id={rewward.Id}, amount={rewward.Amount}");
                }
                return false;
            });
        
        MissionManager.Instance.Subcribe(GetEntityId(),
            MISSION_MESSAGE_TYPE.RUNTIME_INIT,
            (args) =>
            {
                MissionRuntimeBase mission = args[0] as MissionRuntimeBase;
                Debug.Log($"Init: missionId={mission.missionId}, progressValue={mission.progressValue}");
                return false;
            });

        MissionManager.Instance.Subcribe(GetEntityId(),
            MISSION_MESSAGE_TYPE.RUNTIME_PROGRESS,
            (args) =>
            {
                MissionRuntimeBase mission = args[0] as MissionRuntimeBase;
                Debug.Log($"Progress: missionId={mission.missionId}, progressValue={mission.progressValue}");
                return false;
            });

        MissionManager.Instance.Subcribe(GetEntityId(),
            MISSION_MESSAGE_TYPE.RUNTIME_CLAIMABLE,
            (args) =>
            {
                MissionRuntimeBase mission = args[0] as MissionRuntimeBase;
                Debug.Log($"missionId={mission.missionId}, RUNTIME_CLAIMABLE");
                return false;
            });

        MissionManager.Instance.Subcribe(GetEntityId(),
            MISSION_MESSAGE_TYPE.RUNTIME_REWARDED,
            (args) =>
            {
                MissionRuntimeBase mission = args[0] as MissionRuntimeBase;
                RewardData[] rewards = args[1] as RewardData[];
                foreach (var rewward in rewards)
                {
                    Debug.Log($"type={rewward.Type}, id={rewward.Id}, amount={rewward.Amount}");
                }

                GameMessageManager.Instance.Notify(GAME_MESSAGE_TYPE.MISSION_CLEAR, 1);
                return false;
            });
    }
}
