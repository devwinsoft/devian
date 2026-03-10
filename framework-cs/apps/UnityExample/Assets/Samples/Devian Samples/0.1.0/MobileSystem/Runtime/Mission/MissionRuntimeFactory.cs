using System;
using Devian.Domain.Game;

namespace Devian
{
    public struct DailyMissionRuntimeCreateArgs
    {
        public string MissionId { get; set; }
        public string MessageId { get; set; }
        public string PeriodKey { get; set; }
        public int MissionUid { get; set; }
        public int Index { get; set; }
        public GAME_MESSAGE_TYPE StatType { get; set; }
        public GAME_MESSAGE_SAVE_TYPE OpType { get; set; }
        public GAME_MESSAGE_OP_TYPE ConditionOpType { get; set; }
        public CBigInt ConditionValue { get; set; }
        public Action<int, GAME_MESSAGE_TYPE, BaseTrigger<int, GAME_MESSAGE_TYPE>.Handler> SubscribeTrigger { get; set; }
        public Action<int> UnsubscribeTrigger { get; set; }
        public Func<CBigInt> ReadExternalProgress { get; set; }
        public Action<MissionRuntimeBase> OnChanged { get; set; }
        public Action<MissionRuntimeBase> OnClaimable { get; set; }
    }

    public struct PeriodMissionRuntimeCreateArgs
    {
        public string MissionId { get; set; }
        public string MessageId { get; set; }
        public string PeriodKey { get; set; }
        public int MissionUid { get; set; }
        public int Day { get; set; }
        public bool IsWaiting { get; set; }
        public GAME_MESSAGE_TYPE StatType { get; set; }
        public GAME_MESSAGE_SAVE_TYPE OpType { get; set; }
        public GAME_MESSAGE_OP_TYPE ConditionOpType { get; set; }
        public CBigInt ConditionValue { get; set; }
        public Action<int, GAME_MESSAGE_TYPE, BaseTrigger<int, GAME_MESSAGE_TYPE>.Handler> SubscribeTrigger { get; set; }
        public Action<int> UnsubscribeTrigger { get; set; }
        public Func<CBigInt> ReadExternalProgress { get; set; }
        public Action<MissionRuntimeBase> OnChanged { get; set; }
        public Action<MissionRuntimeBase> OnClaimable { get; set; }
    }

    public struct MissionRuntimeRestoreArgs
    {
        public MISSION_TYPE MissionType { get; set; }
        public string MissionId { get; set; }
        public string MessageId { get; set; }
        public string PeriodKey { get; set; }
        public int MissionUid { get; set; }
        public CBigInt ProgressValue { get; set; }
        public bool IsCompleted { get; set; }
        public bool IsWaiting { get; set; }
        public int Index { get; set; }
        public int Day { get; set; }
        public GAME_MESSAGE_TYPE StatType { get; set; }
        public GAME_MESSAGE_SAVE_TYPE OpType { get; set; }
        public GAME_MESSAGE_OP_TYPE ConditionOpType { get; set; }
        public CBigInt ConditionValue { get; set; }
        public Action<int, GAME_MESSAGE_TYPE, BaseTrigger<int, GAME_MESSAGE_TYPE>.Handler> SubscribeTrigger { get; set; }
        public Action<int> UnsubscribeTrigger { get; set; }
        public Func<CBigInt> ReadExternalProgress { get; set; }
        public Action<MissionRuntimeBase> OnChanged { get; set; }
        public Action<MissionRuntimeBase> OnClaimable { get; set; }
    }

    public static class MissionRuntimeFactory
    {
        public static MissionRuntimeDaily CreateDaily(DailyMissionRuntimeCreateArgs args)
        {
            var runtime = new MissionRuntimeDaily
            {
                missionId = args.MissionId ?? string.Empty,
                messageId = args.MessageId ?? string.Empty,
                periodKey = args.PeriodKey ?? string.Empty,
                missionUid = args.MissionUid,
                index = args.Index,
                progressValue = CBigInt.Zero,
                isWaiting = false,
                isCompleted = false,
            };

            runtime.Bind(
                args.MessageId,
                args.StatType,
                args.OpType,
                args.ConditionOpType,
                args.ConditionValue,
                args.SubscribeTrigger,
                args.UnsubscribeTrigger,
                args.ReadExternalProgress,
                args.OnChanged,
                args.OnClaimable);

            return runtime;
        }

        public static MissionRuntimePeriod CreatePeriod(PeriodMissionRuntimeCreateArgs args)
        {
            var runtime = new MissionRuntimePeriod
            {
                missionId = args.MissionId ?? string.Empty,
                messageId = args.MessageId ?? string.Empty,
                periodKey = args.PeriodKey ?? string.Empty,
                missionUid = args.MissionUid,
                day = Math.Clamp(args.Day, 1, 7),
                progressValue = CBigInt.Zero,
                isWaiting = args.IsWaiting,
                isCompleted = false,
            };

            runtime.Bind(
                args.MessageId,
                args.StatType,
                args.OpType,
                args.ConditionOpType,
                args.ConditionValue,
                args.SubscribeTrigger,
                args.UnsubscribeTrigger,
                args.ReadExternalProgress,
                args.OnChanged,
                args.OnClaimable);

            return runtime;
        }

        public static MissionRuntimeBase Restore(MissionRuntimeRestoreArgs args)
        {
            MissionRuntimeBase runtime;
            switch (args.MissionType)
            {
                case MISSION_TYPE.PERIOD:
                    runtime = new MissionRuntimePeriod
                    {
                        missionId = args.MissionId ?? string.Empty,
                        messageId = args.MessageId ?? string.Empty,
                        periodKey = args.PeriodKey ?? string.Empty,
                        missionUid = args.MissionUid,
                        day = Math.Clamp(args.Day, 1, 7),
                        progressValue = args.ProgressValue,
                        isWaiting = args.IsWaiting && !args.IsCompleted,
                        isCompleted = args.IsCompleted,
                    };
                    break;

                default:
                    runtime = new MissionRuntimeDaily
                    {
                        missionId = args.MissionId ?? string.Empty,
                        messageId = args.MessageId ?? string.Empty,
                        periodKey = args.PeriodKey ?? string.Empty,
                        missionUid = args.MissionUid,
                        index = args.Index,
                        progressValue = args.ProgressValue,
                        isWaiting = false,
                        isCompleted = args.IsCompleted,
                    };
                    break;
            }

            runtime.Bind(
                args.MessageId,
                args.StatType,
                args.OpType,
                args.ConditionOpType,
                args.ConditionValue,
                args.SubscribeTrigger,
                args.UnsubscribeTrigger,
                args.ReadExternalProgress,
                args.OnChanged,
                args.OnClaimable);

            return runtime;
        }
    }
}
