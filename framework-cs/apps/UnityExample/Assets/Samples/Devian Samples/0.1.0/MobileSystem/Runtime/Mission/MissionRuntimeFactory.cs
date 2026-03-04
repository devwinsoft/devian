using System;
using Devian.Domain.Game;

namespace Devian
{
    public struct DailyMissionRuntimeCreateArgs
    {
        public MISSION_TYPE MissionType { get; set; }
        public string MissionId { get; set; }
        public string PeriodKey { get; set; }
        public int MissionUid { get; set; }
        public int Index { get; set; }
        public MISSION_CONDITION_TYPE ConditionType { get; set; }
        public MISSION_OP_TYPE ConditionOp { get; set; }
        public CBigInt ConditionValue { get; set; }
        public MissionTriggerSystem TriggerSystem { get; set; }
        public Action<MissionRuntimeBase> OnChanged { get; set; }
        public Action<MissionRuntimeBase> OnClaimable { get; set; }
    }

    public struct AchieveMissionRuntimeCreateArgs
    {
        public MISSION_TYPE MissionType { get; set; }
        public string MissionId { get; set; }
        public int Level { get; set; }
        public string PeriodKey { get; set; }
        public int MissionUid { get; set; }
        public MISSION_CONDITION_TYPE ConditionType { get; set; }
        public MISSION_OP_TYPE ConditionOp { get; set; }
        public CBigInt ConditionValue { get; set; }
        public MissionTriggerSystem TriggerSystem { get; set; }
        public Action<MissionRuntimeBase> OnChanged { get; set; }
        public Action<MissionRuntimeBase> OnClaimable { get; set; }
    }

    public struct MissionRuntimeRestoreArgs
    {
        public MISSION_TYPE MissionType { get; set; }
        public string MissionId { get; set; }
        public string PeriodKey { get; set; }
        public int MissionUid { get; set; }
        public int Level { get; set; }
        public CBigInt ProgressValue { get; set; }
        public bool IsCompleted { get; set; }
        public int Index { get; set; }
        public MISSION_CONDITION_TYPE ConditionType { get; set; }
        public MISSION_OP_TYPE ConditionOp { get; set; }
        public CBigInt ConditionValue { get; set; }
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
                missionType = args.MissionType,
                missionId = args.MissionId ?? string.Empty,
                periodKey = args.PeriodKey ?? string.Empty,
                missionUid = args.MissionUid,
                index = args.Index,
                progressValue = CBigInt.Zero,
                isCompleted = false,
            };

            runtime.Bind(
                args.ConditionType,
                args.ConditionOp,
                args.ConditionValue,
                args.TriggerSystem,
                args.OnChanged,
                args.OnClaimable);

            return runtime;
        }

        public static MissionRuntimeAchieve CreateAchieve(AchieveMissionRuntimeCreateArgs args)
        {
            var runtime = new MissionRuntimeAchieve
            {
                missionType = args.MissionType,
                missionId = args.MissionId ?? string.Empty,
                periodKey = args.PeriodKey ?? string.Empty,
                missionUid = args.MissionUid,
                level = args.Level,
                progressValue = CBigInt.Zero,
                isCompleted = false,
            };

            runtime.Bind(
                args.ConditionType,
                args.ConditionOp,
                args.ConditionValue,
                args.TriggerSystem,
                args.OnChanged,
                args.OnClaimable);

            return runtime;
        }

        public static MissionRuntimeBase Restore(MissionRuntimeRestoreArgs args)
        {
            switch (args.MissionType)
            {
                case MISSION_TYPE.DAY:
                {
                    var runtime = new MissionRuntimeDaily
                    {
                        missionType = args.MissionType,
                        missionId = args.MissionId ?? string.Empty,
                        periodKey = args.PeriodKey ?? string.Empty,
                        missionUid = args.MissionUid,
                        index = args.Index,
                        progressValue = args.ProgressValue,
                        isCompleted = args.IsCompleted,
                    };

                    runtime.Bind(
                        args.ConditionType,
                        args.ConditionOp,
                        args.ConditionValue,
                        args.TriggerSystem,
                        args.OnChanged,
                        args.OnClaimable);

                    return runtime;
                }

                case MISSION_TYPE.ACHIEVE:
                {
                    var runtime = new MissionRuntimeAchieve
                    {
                        missionType = args.MissionType,
                        missionId = args.MissionId ?? string.Empty,
                        periodKey = args.PeriodKey ?? string.Empty,
                        missionUid = args.MissionUid,
                        level = args.Level,
                        progressValue = args.ProgressValue,
                        isCompleted = args.IsCompleted,
                    };

                    runtime.Bind(
                        args.ConditionType,
                        args.ConditionOp,
                        args.ConditionValue,
                        args.TriggerSystem,
                        args.OnChanged,
                        args.OnClaimable);

                    return runtime;
                }

                default:
                    throw new ArgumentOutOfRangeException(nameof(args.MissionType), args.MissionType, "Unsupported mission type for MissionRuntime restore.");
            }
        }
    }
}
