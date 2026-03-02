using System;
using Devian.Domain.Game;

namespace Devian
{
    public struct DailyMissionRuntimeCreateArgs
    {
        public MISSION_TYPE MissionKind { get; set; }
        public string MissionId { get; set; }
        public string PeriodKey { get; set; }
        public int MissionUid { get; set; }
        public MISSION_CONDITION_TYPE ConditionType { get; set; }
        public MISSION_OP_TYPE ConditionOp { get; set; }
        public CBigInt ConditionValue { get; set; }
        public string RewardGroupId { get; set; }
        public MissionTriggerSystem TriggerSystem { get; set; }
        public Action<MissionRuntimeBase> OnChanged { get; set; }
        public Action<MissionRuntimeBase> OnClaimable { get; set; }
    }

    public struct AchieveMissionRuntimeCreateArgs
    {
        public MISSION_TYPE MissionKind { get; set; }
        public string MissionId { get; set; }
        public int Level { get; set; }
        public string PeriodKey { get; set; }
        public int MissionUid { get; set; }
        public CBigInt StartValue { get; set; }
        public MISSION_CONDITION_TYPE ConditionType { get; set; }
        public MISSION_OP_TYPE ConditionOp { get; set; }
        public CBigInt ConditionValue { get; set; }
        public string RewardGroupId { get; set; }
        public MissionTriggerSystem TriggerSystem { get; set; }
        public Action<MissionRuntimeBase> OnChanged { get; set; }
        public Action<MissionRuntimeBase> OnClaimable { get; set; }
    }

    public struct MissionRuntimeRestoreArgs
    {
        public MISSION_TYPE MissionKind { get; set; }
        public string MissionId { get; set; }
        public string PeriodKey { get; set; }
        public int MissionUid { get; set; }
        public int Level { get; set; }
        public CBigInt StartValue { get; set; }
        public CBigInt ProgressValue { get; set; }
        public bool IsCompleted { get; set; }
        public MISSION_CONDITION_TYPE ConditionType { get; set; }
        public MISSION_OP_TYPE ConditionOp { get; set; }
        public CBigInt ConditionValue { get; set; }
        public string RewardGroupId { get; set; }
        public MissionTriggerSystem TriggerSystem { get; set; }
        public Action<MissionRuntimeBase> OnChanged { get; set; }
        public Action<MissionRuntimeBase> OnClaimable { get; set; }
    }

    public static class MissionRuntimeFactory
    {
        public static MissionRuntimeDaily CreateDaily(DailyMissionRuntimeCreateArgs args)
        {
            var runtime = new MissionRuntimeDaily
            {
                missionKind = args.MissionKind,
                missionId = args.MissionId ?? string.Empty,
                periodKey = args.PeriodKey ?? string.Empty,
                missionUid = args.MissionUid,
                progressValue = CBigInt.Zero,
                isCompleted = false,
                rewardGroupId = args.RewardGroupId ?? string.Empty,
            };

            runtime.Bind(
                args.ConditionType,
                args.ConditionOp,
                args.ConditionValue,
                args.RewardGroupId,
                args.TriggerSystem,
                args.OnChanged,
                args.OnClaimable);

            return runtime;
        }

        public static MissionRuntimeAchieve CreateAchieve(AchieveMissionRuntimeCreateArgs args)
        {
            var runtime = new MissionRuntimeAchieve
            {
                missionKind = args.MissionKind,
                missionId = args.MissionId ?? string.Empty,
                periodKey = args.PeriodKey ?? string.Empty,
                missionUid = args.MissionUid,
                level = args.Level,
                startValue = args.StartValue,
                progressValue = args.StartValue,
                isCompleted = false,
                rewardGroupId = args.RewardGroupId ?? string.Empty,
            };

            runtime.Bind(
                args.ConditionType,
                args.ConditionOp,
                args.ConditionValue,
                args.RewardGroupId,
                args.TriggerSystem,
                args.OnChanged,
                args.OnClaimable);

            return runtime;
        }

        public static MissionRuntimeBase Restore(MissionRuntimeRestoreArgs args)
        {
            switch (args.MissionKind)
            {
                case MISSION_TYPE.DAILY:
                {
                    var runtime = new MissionRuntimeDaily
                    {
                        missionKind = args.MissionKind,
                        missionId = args.MissionId ?? string.Empty,
                        periodKey = args.PeriodKey ?? string.Empty,
                        missionUid = args.MissionUid,
                        progressValue = args.ProgressValue,
                        isCompleted = args.IsCompleted,
                        rewardGroupId = args.RewardGroupId ?? string.Empty,
                    };

                    runtime.Bind(
                        args.ConditionType,
                        args.ConditionOp,
                        args.ConditionValue,
                        args.RewardGroupId,
                        args.TriggerSystem,
                        args.OnChanged,
                        args.OnClaimable);

                    return runtime;
                }

                case MISSION_TYPE.ACHIEVEMENT:
                default:
                {
                    var runtime = new MissionRuntimeAchieve
                    {
                        missionKind = args.MissionKind,
                        missionId = args.MissionId ?? string.Empty,
                        periodKey = args.PeriodKey ?? string.Empty,
                        missionUid = args.MissionUid,
                        level = args.Level,
                        startValue = args.StartValue,
                        progressValue = args.ProgressValue,
                        isCompleted = args.IsCompleted,
                        rewardGroupId = args.RewardGroupId ?? string.Empty,
                    };

                    runtime.Bind(
                        args.ConditionType,
                        args.ConditionOp,
                        args.ConditionValue,
                        args.RewardGroupId,
                        args.TriggerSystem,
                        args.OnChanged,
                        args.OnClaimable);

                    return runtime;
                }
            }
        }
    }
}
