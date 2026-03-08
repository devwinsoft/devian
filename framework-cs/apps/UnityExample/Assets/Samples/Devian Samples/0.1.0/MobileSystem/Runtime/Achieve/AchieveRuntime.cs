using System;
using Devian.Domain.Game;

namespace Devian
{
    [Serializable]
    public abstract class AchieveRuntimeBase
    {
        public string achieveId = string.Empty;
        public string messageId = string.Empty;
        public int achieveUid;
        public int level = 1;
        public int index;
        public CBigInt progressValue = CBigInt.Zero;
        public bool isWaiting;
        public bool isCompleted;

        [NonSerialized] private GAME_MESSAGE_TYPE _statType;
        [NonSerialized] private GAME_MESSAGE_SAVE_TYPE _opType;
        [NonSerialized] private CBigInt _conditionValue = CBigInt.Zero;
        [NonSerialized] private Action<AchieveRuntimeBase> _onProgress;
        [NonSerialized] private Action<AchieveRuntimeBase> _onClaimable;
        [NonSerialized] private Func<CBigInt> _externalProgressReader;

        public abstract ACHIEVE_TYPE RuntimeType { get; }

        public GAME_MESSAGE_TYPE StatType => _statType;
        public GAME_MESSAGE_SAVE_TYPE OpType => _opType;
        public CBigInt ConditionValue => _conditionValue;
        public bool IsClaimable => !isCompleted && !isWaiting && progressValue >= _conditionValue;
        public int Index => index;

        internal void Bind(
            string nextMessageId,
            GAME_MESSAGE_TYPE statType,
            GAME_MESSAGE_SAVE_TYPE opType,
            CBigInt conditionValue,
            Func<CBigInt> readExternalProgress,
            Action<AchieveRuntimeBase> onProgress,
            Action<AchieveRuntimeBase> onClaimable)
        {
            messageId = nextMessageId ?? string.Empty;
            isWaiting = false;
            _statType = statType;
            _opType = opType;
            _conditionValue = conditionValue;
            _externalProgressReader = readExternalProgress;
            _onProgress = onProgress;
            _onClaimable = onClaimable;

            RefreshProgressFromExternal(emitProgressEvent: false);

            if (IsClaimable)
                _onClaimable?.Invoke(this);
        }

        internal void BindWaiting(
            string nextMessageId,
            Action<AchieveRuntimeBase> onProgress,
            Action<AchieveRuntimeBase> onClaimable)
        {
            messageId = nextMessageId ?? string.Empty;
            isWaiting = !isCompleted;
            _statType = GAME_MESSAGE_TYPE.NONE;
            _opType = GAME_MESSAGE_SAVE_TYPE.NONE;
            _conditionValue = CBigInt.Zero;
            _externalProgressReader = null;
            _onProgress = onProgress;
            _onClaimable = onClaimable;
            progressValue = CBigInt.Zero;
        }

        public void Detach()
        {
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
        }

        public void LevelUp(
            int nextLevel,
            int nextIndex,
            string nextMessageId,
            GAME_MESSAGE_TYPE nextStatType,
            GAME_MESSAGE_SAVE_TYPE nextOpType,
            CBigInt nextConditionValue,
            Func<CBigInt> readExternalProgress)
        {
            level = nextLevel;
            index = nextIndex;
            isWaiting = false;
            isCompleted = false;
            messageId = nextMessageId ?? string.Empty;
            _statType = nextStatType;
            _opType = nextOpType;
            _conditionValue = nextConditionValue;
            _externalProgressReader = readExternalProgress;

            if (nextOpType == GAME_MESSAGE_SAVE_TYPE.SESSION_SUM)
                progressValue = CBigInt.Zero;
            else if (isTotalSaveType(nextOpType))
                RefreshProgressFromExternal(emitProgressEvent: false);
            else if (nextOpType == GAME_MESSAGE_SAVE_TYPE.NONE && nextConditionValue.CompareTo(CBigInt.Zero) <= 0)
                progressValue = CBigInt.Zero;

            RaiseClaimableIfNeeded();
        }

        public void LevelUpToWaiting(int nextLevel, int nextIndex, string nextMessageId)
        {
            level = nextLevel;
            index = nextIndex;
            isCompleted = false;
            isWaiting = true;
            messageId = nextMessageId ?? string.Empty;
            _statType = GAME_MESSAGE_TYPE.NONE;
            _opType = GAME_MESSAGE_SAVE_TYPE.NONE;
            _conditionValue = CBigInt.Zero;
            _externalProgressReader = null;
            progressValue = CBigInt.Zero;
        }

        void RaiseChanged()
        {
            _onProgress?.Invoke(this);
        }

        void RaiseClaimableIfNeeded()
        {
            if (IsClaimable)
                _onClaimable?.Invoke(this);
        }

        void RefreshProgressFromExternal(bool emitProgressEvent)
        {
            if (_externalProgressReader == null)
                return;

            var wasClaimable = IsClaimable;
            var nextProgress = _externalProgressReader();
            if (nextProgress.CompareTo(progressValue) == 0)
                return;

            progressValue = nextProgress;
            if (!emitProgressEvent)
                return;

            RaiseChanged();
            if (!wasClaimable && IsClaimable)
                RaiseClaimableIfNeeded();
        }

        internal void OnMessageStatUpdated(GAME_MESSAGE_TYPE messageType, CBigInt delta)
        {
            if (isCompleted || isWaiting || _opType == GAME_MESSAGE_SAVE_TYPE.NONE || _statType != messageType)
                return;

            if (isTotalSaveType(_opType))
            {
                RefreshProgressFromExternal(emitProgressEvent: true);
                return;
            }

            var wasClaimable = IsClaimable;
            CBigInt nextProgress;
            switch (_opType)
            {
                case GAME_MESSAGE_SAVE_TYPE.SESSION_SUM:
                    nextProgress = progressValue + delta;
                    break;

                case GAME_MESSAGE_SAVE_TYPE.SESSION_MAX:
                    nextProgress = CBigInt.Max(progressValue, delta);
                    break;

                default:
                    return;
            }

            if (nextProgress.CompareTo(progressValue) == 0)
                return;

            progressValue = nextProgress;
            RaiseChanged();
            if (!wasClaimable && IsClaimable)
                RaiseClaimableIfNeeded();
        }

        static bool isTotalSaveType(GAME_MESSAGE_SAVE_TYPE saveType)
        {
            return saveType == GAME_MESSAGE_SAVE_TYPE.TOTAL_SUM
                   || saveType == GAME_MESSAGE_SAVE_TYPE.TOTAL_MAX;
        }
    }

    [Serializable]
    public sealed class AchieveRuntimeOnce : AchieveRuntimeBase
    {
        public override ACHIEVE_TYPE RuntimeType => ACHIEVE_TYPE.ONCE;
    }

    [Serializable]
    public sealed class AchieveRuntimePass : AchieveRuntimeBase
    {
        public override ACHIEVE_TYPE RuntimeType => ACHIEVE_TYPE.PASS;
    }
}
