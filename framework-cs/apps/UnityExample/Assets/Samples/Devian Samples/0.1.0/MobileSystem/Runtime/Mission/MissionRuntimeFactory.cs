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
        public MESSAGE_META_TYPE StatType { get; set; }
        public MESSAGE_META_SAVE_TYPE OpType { get; set; }
        public MESSAGE_META_OP_TYPE ConditionOpType { get; set; }
        public CBigInt ConditionValue { get; set; }
        public Action<int, MESSAGE_META_TYPE, BaseTrigger<int, MESSAGE_META_TYPE>.Handler> SubscribeTrigger { get; set; }
        public Action<int> UnsubscribeTrigger { get; set; }
        public Func<CBigInt> ReadExternalProgress { get; set; }
        public Action<MissionRuntimeBase> OnChanged { get; set; }
        public Action<MissionRuntimeBase> OnClaimable { get; set; }
    }

    public struct MissionRuntimeRestoreArgs
    {
        public string MissionId { get; set; }
        public string MessageId { get; set; }
        public string PeriodKey { get; set; }
        public int MissionUid { get; set; }
        public CBigInt ProgressValue { get; set; }
        public bool IsCompleted { get; set; }
        public int Index { get; set; }
        public MESSAGE_META_TYPE StatType { get; set; }
        public MESSAGE_META_SAVE_TYPE OpType { get; set; }
        public MESSAGE_META_OP_TYPE ConditionOpType { get; set; }
        public CBigInt ConditionValue { get; set; }
        public Action<int, MESSAGE_META_TYPE, BaseTrigger<int, MESSAGE_META_TYPE>.Handler> SubscribeTrigger { get; set; }
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
            var runtime = new MissionRuntimeDaily
            {
                missionId = args.MissionId ?? string.Empty,
                messageId = args.MessageId ?? string.Empty,
                periodKey = args.PeriodKey ?? string.Empty,
                missionUid = args.MissionUid,
                index = args.Index,
                progressValue = args.ProgressValue,
                isCompleted = args.IsCompleted,
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
    }
}
