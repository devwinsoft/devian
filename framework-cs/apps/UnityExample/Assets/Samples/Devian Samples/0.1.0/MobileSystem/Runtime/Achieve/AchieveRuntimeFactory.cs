using System;
using Devian.Domain.Game;

namespace Devian
{
    public struct AchieveRuntimeCreateArgs
    {
        public string AchieveId { get; set; }
        public string MessageId { get; set; }
        public int Level { get; set; }
        public int AchieveUid { get; set; }
        public GAME_MESSAGE_TYPE StatType { get; set; }
        public GAME_MESSAGE_SAVE_TYPE OpType { get; set; }
        public CBigInt ConditionValue { get; set; }
        public Func<CBigInt> ReadProgress { get; set; }
        public Action<AchieveRuntime> OnChanged { get; set; }
        public Action<AchieveRuntime> OnClaimable { get; set; }
    }

    public struct AchieveRuntimeRestoreArgs
    {
        public string AchieveId { get; set; }
        public string MessageId { get; set; }
        public int Level { get; set; }
        public int AchieveUid { get; set; }
        public CBigInt ProgressValue { get; set; }
        public bool IsCompleted { get; set; }
        public GAME_MESSAGE_TYPE StatType { get; set; }
        public GAME_MESSAGE_SAVE_TYPE OpType { get; set; }
        public CBigInt ConditionValue { get; set; }
        public Func<CBigInt> ReadProgress { get; set; }
        public Action<AchieveRuntime> OnChanged { get; set; }
        public Action<AchieveRuntime> OnClaimable { get; set; }
    }

    public static class AchieveRuntimeFactory
    {
        public static AchieveRuntime Create(AchieveRuntimeCreateArgs args)
        {
            var runtime = new AchieveRuntime
            {
                achieveId = args.AchieveId ?? string.Empty,
                messageId = args.MessageId ?? string.Empty,
                achieveUid = args.AchieveUid,
                level = args.Level,
                progressValue = CBigInt.Zero,
                isCompleted = false,
            };

            runtime.Bind(
                args.MessageId,
                args.StatType,
                args.OpType,
                args.ConditionValue,
                args.ReadProgress,
                args.OnChanged,
                args.OnClaimable);

            return runtime;
        }

        public static AchieveRuntime Restore(AchieveRuntimeRestoreArgs args)
        {
            var runtime = new AchieveRuntime
            {
                achieveId = args.AchieveId ?? string.Empty,
                messageId = args.MessageId ?? string.Empty,
                achieveUid = args.AchieveUid,
                level = args.Level,
                progressValue = args.ProgressValue,
                isCompleted = args.IsCompleted,
            };

            runtime.Bind(
                args.MessageId,
                args.StatType,
                args.OpType,
                args.ConditionValue,
                args.ReadProgress,
                args.OnChanged,
                args.OnClaimable);

            return runtime;
        }
    }
}
