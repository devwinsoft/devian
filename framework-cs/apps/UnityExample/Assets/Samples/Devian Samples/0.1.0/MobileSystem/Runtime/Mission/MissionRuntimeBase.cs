using System;
using Devian.Domain.Game;

namespace Devian
{
    [Serializable]
    public abstract class MissionRuntimeBase
    {
        public string missionId = string.Empty;
        public string messageId = string.Empty;
        public string periodKey = string.Empty;
        public int missionUid;
        public CBigInt progressValue = CBigInt.Zero;
        public bool isWaiting;
        public bool isCompleted;

        [NonSerialized] protected MESSAGE_META_TYPE _statType;
        [NonSerialized] protected MESSAGE_META_SAVE_TYPE _opType;
        [NonSerialized] protected MESSAGE_META_OP_TYPE _conditionOpType = MESSAGE_META_OP_TYPE.GTE;
        [NonSerialized] protected CBigInt _conditionValue = CBigInt.Zero;
        [NonSerialized] private bool _hasSessionMinSample;
        [NonSerialized] private Action<int, MESSAGE_META_TYPE, BaseTrigger<int, MESSAGE_META_TYPE>.Handler> _subscribeTrigger;
        [NonSerialized] private Action<int> _unsubscribeTrigger;
        [NonSerialized] private Action<MissionRuntimeBase> _onProgress;
        [NonSerialized] private Action<MissionRuntimeBase> _onClaimable;
        [NonSerialized] private Func<CBigInt> _externalProgressReader;
        [NonSerialized] private bool _isSubscribed;

        public MESSAGE_META_TYPE StatType => _statType;
        public MESSAGE_META_SAVE_TYPE OpType => _opType;
        public MESSAGE_META_OP_TYPE ConditionOpType => _conditionOpType;
        public CBigInt ConditionValue => _conditionValue;
        public bool IsSubscribed => _isSubscribed;
        public bool IsClaimable => !isCompleted
                                   && !isWaiting
                                   && GameMessageRule.IsConditionSatisfied(
                                       progressValue,
                                       _conditionOpType,
                                       _conditionValue);
        public abstract int Index { get; }

        internal void Bind(
            string messageId,
            MESSAGE_META_TYPE statType,
            MESSAGE_META_SAVE_TYPE opType,
            MESSAGE_META_OP_TYPE conditionOpType,
            CBigInt conditionValue,
            Action<int, MESSAGE_META_TYPE, BaseTrigger<int, MESSAGE_META_TYPE>.Handler> subscribeTrigger,
            Action<int> unsubscribeTrigger,
            Func<CBigInt> readExternalProgress,
            Action<MissionRuntimeBase> onProgress,
            Action<MissionRuntimeBase> onClaimable)
        {
            UnsubscribeInternal();

            this.messageId = messageId ?? string.Empty;
            _statType = statType;
            _opType = opType;
            _conditionOpType = conditionOpType;
            _conditionValue = conditionValue;
            _hasSessionMinSample = opType == MESSAGE_META_SAVE_TYPE.SESSION_MIN
                                   && progressValue.CompareTo(CBigInt.Zero) != 0;
            _subscribeTrigger = subscribeTrigger;
            _unsubscribeTrigger = unsubscribeTrigger;
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
            _subscribeTrigger = null;
            _unsubscribeTrigger = null;
            _externalProgressReader = null;
            _onProgress = null;
            _onClaimable = null;
        }

        public MissionRuntimeState GetState()
        {
            if (isCompleted)
                return MissionRuntimeState.COMPLETED;

            if (isWaiting)
                return MissionRuntimeState.WAIT;

            return IsClaimable
                ? MissionRuntimeState.CLAIMABLE
                : MissionRuntimeState.ACTIVE;
        }

        public void MarkCompleted()
        {
            isWaiting = false;
            isCompleted = true;
            OnCompletedCore();
        }

        public bool TryActivate()
        {
            if (isCompleted || !isWaiting)
                return false;

            isWaiting = false;
            RefreshProgressFromExternal(emitProgressEvent: false);
            SubscribeIfNeeded();
            RaiseClaimableIfNeeded();
            return true;
        }

        protected void SubscribeIfNeeded()
        {
            if (_isSubscribed || !ShouldSubscribe() || _subscribeTrigger == null)
                return;

            _subscribeTrigger.Invoke(missionUid, _statType, handleTrigger);
            _isSubscribed = true;
        }

        protected void UnsubscribeInternal()
        {
            if (!_isSubscribed)
                return;

            _unsubscribeTrigger?.Invoke(missionUid);
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

        protected virtual bool ShouldSubscribe()
        {
            return _opType != MESSAGE_META_SAVE_TYPE.NONE
                   && !isCompleted
                   && !isWaiting;
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
            nextProgress = GameMessageRule.ClampNonNegative(nextProgress);
            if (nextProgress.CompareTo(progressValue) == 0)
                return;

            progressValue = nextProgress;
            if (!emitProgressEvent)
                return;

            RaiseChanged();
            if (!wasClaimable && IsClaimable)
                RaiseClaimableIfNeeded();
        }

        bool handleTrigger(object[] args)
        {
            if (!TryReadProgressDelta(args, out var delta))
                return false;

            if (isCompleted || isWaiting || _opType == MESSAGE_META_SAVE_TYPE.NONE)
                return false;

            var wasClaimable = IsClaimable;
            var nextProgress = calculateNextProgress(delta);
            nextProgress = GameMessageRule.ClampNonNegative(nextProgress);
            if (nextProgress.CompareTo(progressValue) == 0)
                return false;

            progressValue = nextProgress;
            RaiseChanged();
            if (!wasClaimable && IsClaimable)
                RaiseClaimableIfNeeded();

            return false;
        }

        CBigInt calculateNextProgress(CBigInt delta)
        {
            if (_externalProgressReader != null)
                return readExternalProgress();

            switch (_opType)
            {
                case MESSAGE_META_SAVE_TYPE.SESSION_MAX:
                    if (delta.CompareTo(CBigInt.Zero) < 0)
                        return progressValue;
                    return CBigInt.Max(progressValue, delta);

                case MESSAGE_META_SAVE_TYPE.SESSION_SUM:
                    return CalculateSumProgress(delta);

                case MESSAGE_META_SAVE_TYPE.SESSION_MIN:
                    if (delta.CompareTo(CBigInt.Zero) < 0)
                        return progressValue;

                    if (!_hasSessionMinSample)
                    {
                        _hasSessionMinSample = true;
                        return delta;
                    }

                    return CBigInt.Min(progressValue, delta);

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

        static bool TryReadProgressDelta(object[] args, out CBigInt value)
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
            return base.ShouldSubscribe() && !IsClaimable;
        }

        protected override void OnCompletedCore()
        {
            UnsubscribeInternal();
        }

        protected override CBigInt CalculateSumProgress(CBigInt delta)
        {
            var next = GameMessageRule.ClampNonNegative(progressValue + delta);
            return CBigInt.Min(_conditionValue, next);
        }
    }

    [Serializable]
    public sealed class MissionRuntimePeriod : MissionRuntimeBase
    {
        public int day = 1;

        public override int Index => day;

        protected override bool ShouldSubscribe()
        {
            return base.ShouldSubscribe() && !IsClaimable;
        }

        protected override void OnCompletedCore()
        {
            UnsubscribeInternal();
        }

        protected override CBigInt CalculateSumProgress(CBigInt delta)
        {
            var next = GameMessageRule.ClampNonNegative(progressValue + delta);
            return CBigInt.Min(_conditionValue, next);
        }
    }
}
