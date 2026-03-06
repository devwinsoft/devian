using System;
using Devian.Domain.Game;

namespace Devian
{
    [Serializable]
    public abstract class MissionRuntimeBase
    {
        public MISSION_TYPE missionType;
        public string missionId = string.Empty;
        public string missionStatId = string.Empty;
        public string periodKey = string.Empty;
        public int missionUid;
        public CBigInt progressValue = CBigInt.Zero;
        public bool isCompleted;

        [NonSerialized] protected MISSION_STAT_TYPE _statType;
        [NonSerialized] protected MISSION_OP_TYPE _opType;
        [NonSerialized] protected CBigInt _conditionValue = CBigInt.Zero;
        [NonSerialized] protected MissionTriggerSystem _triggerSystem;
        [NonSerialized] private Action<MissionRuntimeBase> _onProgress;
        [NonSerialized] private Action<MissionRuntimeBase> _onClaimable;
        [NonSerialized] private Func<CBigInt> _externalProgressReader;
        [NonSerialized] private bool _isSubscribed;

        public MISSION_STAT_TYPE StatType => _statType;
        public MISSION_OP_TYPE OpType => _opType;
        public CBigInt ConditionValue => _conditionValue;
        public bool IsSubscribed => _isSubscribed;
        public bool IsClaimable => !isCompleted && progressValue >= _conditionValue;
        public abstract int Index { get; }

        internal void Bind(
            string missionStatId,
            MISSION_STAT_TYPE statType,
            MISSION_OP_TYPE opType,
            CBigInt conditionValue,
            MissionTriggerSystem triggerSystem,
            Func<CBigInt> readExternalProgress,
            Action<MissionRuntimeBase> onProgress,
            Action<MissionRuntimeBase> onClaimable)
        {
            UnsubscribeInternal();

            this.missionStatId = missionStatId ?? string.Empty;
            _statType = statType;
            _opType = opType;
            _conditionValue = conditionValue;
            _triggerSystem = triggerSystem;
            _externalProgressReader = readExternalProgress;
            _onProgress = onProgress;
            _onClaimable = onClaimable;

            RefreshProgressFromExternal(emitProgressEvent: false);
            SubscribeIfNeeded();

            if (IsClaimable)
                _onClaimable?.Invoke(this);
        }

        public void Detach()
        {
            UnsubscribeInternal();
            _triggerSystem = null;
            _externalProgressReader = null;
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

            _triggerSystem.Subcribe(missionUid, _statType, handleTrigger);
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
            string missionStatId,
            MISSION_STAT_TYPE statType,
            MISSION_OP_TYPE opType,
            CBigInt conditionValue,
            Func<CBigInt> readExternalProgress)
        {
            this.missionStatId = missionStatId ?? string.Empty;
            _statType = statType;
            _opType = opType;
            _conditionValue = conditionValue;
            _externalProgressReader = readExternalProgress;
        }

        protected virtual bool ShouldSubscribe()
        {
            return _opType != MISSION_OP_TYPE.NONE;
        }

        protected virtual void OnClaimableCore()
        {
        }

        protected virtual void OnCompletedCore()
        {
        }

        protected abstract CBigInt CalculateSumProgress(CBigInt delta);

        protected void RefreshProgressFromExternal(bool emitProgressEvent)
        {
            if (_externalProgressReader == null)
                return;

            var wasClaimable = IsClaimable;
            var nextProgress = readExternalProgress();
            if (nextProgress.CompareTo(progressValue) == 0)
                return;

            progressValue = nextProgress;
            if (!emitProgressEvent)
                return;

            RaiseChanged();
            if (!wasClaimable && IsClaimable)
            {
                OnClaimableCore();
                RaiseClaimableIfNeeded();
            }
        }

        private bool handleTrigger(object[] args)
        {
            if (!TryReadProgressDelta(args, out var delta))
                return false;

            if (_opType == MISSION_OP_TYPE.NONE)
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
            if (_externalProgressReader != null)
                return readExternalProgress();

            switch (_opType)
            {
                case MISSION_OP_TYPE.MAX:
                    return CBigInt.Max(progressValue, delta);

                case MISSION_OP_TYPE.SUM:
                    return CalculateSumProgress(delta);

                default:
                    return progressValue;
            }
        }

        CBigInt readExternalProgress()
        {
            return _externalProgressReader != null
                ? _externalProgressReader()
                : progressValue;
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
            return base.ShouldSubscribe() && !isCompleted;
        }

        protected override void OnCompletedCore()
        {
            UnsubscribeInternal();
        }

        protected override CBigInt CalculateSumProgress(CBigInt delta)
        {
            return progressValue + delta;
        }

        public void LevelUp(
            int nextLevel,
            string nextMissionStatId,
            MISSION_STAT_TYPE nextStatType,
            MISSION_OP_TYPE nextOpType,
            CBigInt nextConditionValue,
            Func<CBigInt> readExternalProgress)
        {
            UnsubscribeInternal();

            level = nextLevel;
            isCompleted = false;
            ReplaceBinding(nextMissionStatId, nextStatType, nextOpType, nextConditionValue, readExternalProgress);
            RefreshProgressFromExternal(emitProgressEvent: false);

            SubscribeIfNeeded();
            RaiseClaimableIfNeeded();
        }
    }
}
