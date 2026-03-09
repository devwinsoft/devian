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
        public bool isCompleted;

        [NonSerialized] protected GAME_MESSAGE_TYPE _statType;
        [NonSerialized] protected GAME_MESSAGE_SAVE_TYPE _opType;
        [NonSerialized] protected GAME_MESSAGE_OP_TYPE _conditionOpType = GAME_MESSAGE_OP_TYPE.GTE;
        [NonSerialized] protected CBigInt _conditionValue = CBigInt.Zero;
        [NonSerialized] private bool _hasSessionMinSample;
        [NonSerialized] private Action<int, GAME_MESSAGE_TYPE, BaseTrigger<int, GAME_MESSAGE_TYPE>.Handler> _subscribeTrigger;
        [NonSerialized] private Action<int> _unsubscribeTrigger;
        [NonSerialized] private Action<MissionRuntimeBase> _onProgress;
        [NonSerialized] private Action<MissionRuntimeBase> _onClaimable;
        [NonSerialized] private Func<CBigInt> _externalProgressReader;
        [NonSerialized] private bool _isSubscribed;

        public GAME_MESSAGE_TYPE StatType => _statType;
        public GAME_MESSAGE_SAVE_TYPE OpType => _opType;
        public GAME_MESSAGE_OP_TYPE ConditionOpType => _conditionOpType;
        public CBigInt ConditionValue => _conditionValue;
        public bool IsSubscribed => _isSubscribed;
        public bool IsClaimable => !isCompleted
                                   && GameMessageRule.IsConditionSatisfied(
                                       progressValue,
                                       _conditionOpType,
                                       _conditionValue);
        public abstract int Index { get; }

        internal void Bind(
            string messageId,
            GAME_MESSAGE_TYPE statType,
            GAME_MESSAGE_SAVE_TYPE opType,
            GAME_MESSAGE_OP_TYPE conditionOpType,
            CBigInt conditionValue,
            Action<int, GAME_MESSAGE_TYPE, BaseTrigger<int, GAME_MESSAGE_TYPE>.Handler> subscribeTrigger,
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
            _hasSessionMinSample = opType == GAME_MESSAGE_SAVE_TYPE.SESSION_MIN
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
            return _opType != GAME_MESSAGE_SAVE_TYPE.NONE;
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
                RaiseClaimableIfNeeded();
        }

        bool handleTrigger(object[] args)
        {
            if (!TryReadProgressDelta(args, out var delta))
                return false;

            if (_opType == GAME_MESSAGE_SAVE_TYPE.NONE)
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
                case GAME_MESSAGE_SAVE_TYPE.SESSION_MAX:
                    if (delta.CompareTo(CBigInt.Zero) < 0)
                        return progressValue;
                    return CBigInt.Max(progressValue, delta);

                case GAME_MESSAGE_SAVE_TYPE.SESSION_SUM:
                    return CalculateSumProgress(delta);

                case GAME_MESSAGE_SAVE_TYPE.SESSION_MIN:
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
            return base.ShouldSubscribe() && !isCompleted && !IsClaimable;
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
