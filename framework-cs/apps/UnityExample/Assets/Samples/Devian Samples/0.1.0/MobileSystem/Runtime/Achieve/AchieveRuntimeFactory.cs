using System;
using Devian.Domain.Game;

namespace Devian
{
    public struct AchieveRuntimeCreateArgs
    {
        public ACHIEVE_TYPE AchieveType { get; set; }
        public string AchieveId { get; set; }
        public int Level { get; set; }
        public int AchieveUid { get; set; }
        public int Index { get; set; }
        public bool IsWaiting { get; set; }
        public GAME_MESSAGE_TYPE StatType { get; set; }
        public GAME_MESSAGE_SAVE_TYPE OpType { get; set; }
        public GAME_MESSAGE_OP_TYPE ConditionOpType { get; set; }
        public CBigInt ConditionValue { get; set; }
        public Func<CBigInt> ReadProgress { get; set; }
        public Action<AchieveRuntimeBase> OnChanged { get; set; }
        public Action<AchieveRuntimeBase> OnClaimable { get; set; }
    }

    public struct AchieveRuntimeRestoreArgs
    {
        public ACHIEVE_TYPE AchieveType { get; set; }
        public string AchieveId { get; set; }
        public int Level { get; set; }
        public int AchieveUid { get; set; }
        public int Index { get; set; }
        public bool IsWaiting { get; set; }
        public CBigInt ProgressValue { get; set; }
        public bool IsCompleted { get; set; }
        public GAME_MESSAGE_TYPE StatType { get; set; }
        public GAME_MESSAGE_SAVE_TYPE OpType { get; set; }
        public GAME_MESSAGE_OP_TYPE ConditionOpType { get; set; }
        public CBigInt ConditionValue { get; set; }
        public Func<CBigInt> ReadProgress { get; set; }
        public Action<AchieveRuntimeBase> OnChanged { get; set; }
        public Action<AchieveRuntimeBase> OnClaimable { get; set; }
    }

    public static class AchieveRuntimeFactory
    {
        public static AchieveRuntimeBase Create(AchieveRuntimeCreateArgs args)
        {
            var runtime = createRuntime(args.AchieveType);
            if (runtime == null)
                return null;

            runtime.achieveId = args.AchieveId ?? string.Empty;
            runtime.achieveUid = args.AchieveUid;
            runtime.level = args.Level;
            runtime.index = args.Index;
            runtime.progressValue = CBigInt.Zero;
            runtime.isWaiting = args.IsWaiting;
            runtime.isCompleted = false;

            if (args.IsWaiting)
            {
                runtime.BindWaiting(
                    args.OnChanged,
                    args.OnClaimable);
            }
            else
            {
                runtime.Bind(
                    args.StatType,
                    args.OpType,
                    args.ConditionOpType,
                    args.ConditionValue,
                    args.ReadProgress,
                    args.OnChanged,
                    args.OnClaimable);
            }

            return runtime;
        }

        public static AchieveRuntimeBase Restore(AchieveRuntimeRestoreArgs args)
        {
            var runtime = createRuntime(args.AchieveType);
            if (runtime == null)
                return null;

            runtime.achieveId = args.AchieveId ?? string.Empty;
            runtime.achieveUid = args.AchieveUid;
            runtime.level = args.Level;
            runtime.index = args.Index;
            runtime.progressValue = args.ProgressValue;
            runtime.isWaiting = args.IsWaiting && !args.IsCompleted;
            runtime.isCompleted = args.IsCompleted;

            if (args.IsWaiting && !args.IsCompleted)
            {
                runtime.BindWaiting(
                    args.OnChanged,
                    args.OnClaimable);
            }
            else
            {
                runtime.Bind(
                    args.StatType,
                    args.OpType,
                    args.ConditionOpType,
                    args.ConditionValue,
                    args.ReadProgress,
                    args.OnChanged,
                    args.OnClaimable);
            }

            return runtime;
        }

        static AchieveRuntimeBase createRuntime(ACHIEVE_TYPE achieveType)
        {
            switch (achieveType)
            {
                case ACHIEVE_TYPE.ONCE:
                    return new AchieveRuntimeOnce();

                case ACHIEVE_TYPE.PASS:
                    return new AchieveRuntimePass();

                default:
                    return null;
            }
        }
    }
}
