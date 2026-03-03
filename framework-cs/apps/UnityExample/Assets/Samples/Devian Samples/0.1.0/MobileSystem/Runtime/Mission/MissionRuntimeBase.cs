using System;
using Devian.Domain.Game;

namespace Devian
{
    [Serializable]
    public abstract class MissionRuntimeBase
    {
        public MISSION_TYPE missionType;
        public string missionId = string.Empty;
        public string periodKey = string.Empty;
        public int missionUid;
        public CBigInt progressValue = CBigInt.Zero;
        public bool isCompleted;

        [NonSerialized] protected MISSION_CONDITION_TYPE _conditionType;
        [NonSerialized] protected MISSION_OP_TYPE _conditionOp;
        [NonSerialized] protected CBigInt _conditionValue = CBigInt.Zero;
        [NonSerialized] protected MissionTriggerSystem _triggerSystem;
        [NonSerialized] private Action<MissionRuntimeBase> _onProgress;
        [NonSerialized] private Action<MissionRuntimeBase> _onClaimable;
        [NonSerialized] private bool _isSubscribed;

        public MISSION_CONDITION_TYPE ConditionType => _conditionType;
        public MISSION_OP_TYPE ConditionOp => _conditionOp;
        public CBigInt ConditionValue => _conditionValue;
        public bool IsSubscribed => _isSubscribed;
        public bool IsClaimable => !isCompleted && progressValue >= _conditionValue;
        public abstract int Index { get; }

        internal void Bind(
            MISSION_CONDITION_TYPE conditionType,
            MISSION_OP_TYPE conditionOp,
            CBigInt conditionValue,
            MissionTriggerSystem triggerSystem,
            Action<MissionRuntimeBase> onProgress,
            Action<MissionRuntimeBase> onClaimable)
        {
            UnsubscribeInternal();

            _conditionType = conditionType;
            _conditionOp = conditionOp;
            _conditionValue = conditionValue;
            _triggerSystem = triggerSystem;
            _onProgress = onProgress;
            _onClaimable = onClaimable;

            SubscribeIfNeeded();

            if (IsClaimable)
                _onClaimable?.Invoke(this);
        }

        public void Detach()
        {
            UnsubscribeInternal();
            _triggerSystem = null;
            _onProgress = null;
            _onClaimable = null;
        }

        public MissionRuntimeState GetState()
        {
            if (isCompleted)
                return MissionRuntimeState.COMPLETED;

            return IsClaimable
                ? MissionRuntimeState.CLAIMABLE
                : MissionRuntimeState.ACTIVE;
        }

        public void MarkCompleted()
        {
            isCompleted = true;
            OnCompletedCore();
        }

        protected void SubscribeIfNeeded()
        {
            if (_triggerSystem == null || _isSubscribed || !ShouldSubscribe())
                return;

            _triggerSystem.Subcribe(missionUid, _conditionType, handleTrigger);
            _isSubscribed = true;
        }

        protected void UnsubscribeInternal()
        {
            if (_triggerSystem == null || !_isSubscribed)
                return;

            _triggerSystem.UnSubcribe(missionUid);
            _isSubscribed = false;
        }

        protected void RaiseChanged()
        {
            _onProgress?.Invoke(this);
        }

        protected void RaiseClaimableIfNeeded()
        {
            if (IsClaimable)
                _onClaimable?.Invoke(this);
        }

        protected void ReplaceBinding(
            MISSION_CONDITION_TYPE conditionType,
            MISSION_OP_TYPE conditionOp,
            CBigInt conditionValue)
        {
            _conditionType = conditionType;
            _conditionOp = conditionOp;
            _conditionValue = conditionValue;
        }

        protected virtual bool ShouldSubscribe()
        {
            return _conditionOp != MISSION_OP_TYPE.NONE;
        }

        protected virtual void OnClaimableCore()
        {
        }

        protected virtual void OnCompletedCore()
        {
        }

        protected abstract CBigInt CalculateSumProgress(CBigInt delta);

        private bool handleTrigger(object[] args)
        {
            if (!TryReadProgressDelta(args, out var delta))
                return false;

            if (_conditionOp == MISSION_OP_TYPE.NONE)
                return false;

            var wasClaimable = IsClaimable;
            var nextProgress = calculateNextProgress(delta);
            if (nextProgress.CompareTo(progressValue) == 0)
                return false;

            progressValue = nextProgress;
            RaiseChanged();
            if (!wasClaimable && IsClaimable)
            {
                OnClaimableCore();
                RaiseClaimableIfNeeded();
            }

            return false;
        }

        private CBigInt calculateNextProgress(CBigInt delta)
        {
            switch (_conditionOp)
            {
                case MISSION_OP_TYPE.MAX:
                    return CBigInt.Max(progressValue, delta);

                case MISSION_OP_TYPE.SUM:
                    return CalculateSumProgress(delta);

                default:
                    return progressValue;
            }
        }

        private static bool TryReadProgressDelta(object[] args, out CBigInt value)
        {
            value = CBigInt.Zero;
            if (args == null || args.Length <= 0)
                return false;

            switch (args[0])
            {
                case CBigInt bigInt:
                    value = bigInt;
                    return true;

                case int intValue:
                    value = CBigInt.FromInt(intValue);
                    return true;

                case long longValue:
                    value = CBigInt.FromLong(longValue);
                    return true;

                default:
                    return false;
            }
        }
    }

    [Serializable]
    public sealed class MissionRuntimeDaily : MissionRuntimeBase
    {
        public int index;

        public override int Index => index;

        protected override bool ShouldSubscribe()
        {
            return base.ShouldSubscribe() && !isCompleted && !IsClaimable;
        }

        protected override void OnClaimableCore()
        {
            UnsubscribeInternal();
        }

        protected override void OnCompletedCore()
        {
            UnsubscribeInternal();
        }

        protected override CBigInt CalculateSumProgress(CBigInt delta)
        {
            return CBigInt.Min(_conditionValue, progressValue + delta);
        }
    }

    [Serializable]
    public sealed class MissionRuntimeAchieve : MissionRuntimeBase
    {
        public int level = 1;
        public CBigInt startValue = CBigInt.Zero;

        public override int Index
        {
            get
            {
                var row = TB_MISSION_ACHIEVE.GetByGroup(missionId);
                foreach (var candidate in row)
                {
                    if (candidate.Level == level)
                        return Math.Max(0, candidate.OrderNum - 1);
                }

                return 0;
            }
        }

        protected override bool ShouldSubscribe()
        {
            return base.ShouldSubscribe();
        }

        protected override CBigInt CalculateSumProgress(CBigInt delta)
        {
            return progressValue + delta;
        }

        public void LevelUp(
            int nextLevel,
            CBigInt nextStartValue,
            MISSION_CONDITION_TYPE nextConditionType,
            MISSION_OP_TYPE nextConditionOp,
            CBigInt nextConditionValue)
        {
            UnsubscribeInternal();

            level = nextLevel;
            startValue = nextStartValue;
            isCompleted = false;
            ReplaceBinding(nextConditionType, nextConditionOp, nextConditionValue);

            SubscribeIfNeeded();
            RaiseClaimableIfNeeded();
        }
    }
}
