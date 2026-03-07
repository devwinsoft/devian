using System;
using Devian.Domain.Game;

namespace Devian
{
    [Serializable]
    public sealed class AchieveRuntime
    {
        public string achieveId = string.Empty;
        public string messageId = string.Empty;
        public int achieveUid;
        public int level = 1;
        public CBigInt progressValue = CBigInt.Zero;
        public bool isCompleted;

        [NonSerialized] private GAME_MESSAGE_TYPE _statType;
        [NonSerialized] private GAME_MESSAGE_SAVE_TYPE _opType;
        [NonSerialized] private CBigInt _conditionValue = CBigInt.Zero;
        [NonSerialized] private Action<AchieveRuntime> _onProgress;
        [NonSerialized] private Action<AchieveRuntime> _onClaimable;
        [NonSerialized] private Func<CBigInt> _externalProgressReader;

        public GAME_MESSAGE_TYPE StatType => _statType;
        public GAME_MESSAGE_SAVE_TYPE OpType => _opType;
        public CBigInt ConditionValue => _conditionValue;
        public bool IsClaimable => !isCompleted && progressValue >= _conditionValue;

        public int Index
        {
            get
            {
                var rows = TB_ACHIEVE.GetByGroup(achieveId);
                foreach (var row in rows)
                {
                    if (row.Level == level)
                        return Math.Max(0, row.OrderNum - 1);
                }

                return 0;
            }
        }

        internal void Bind(
            string messageId,
            GAME_MESSAGE_TYPE statType,
            GAME_MESSAGE_SAVE_TYPE opType,
            CBigInt conditionValue,
            Func<CBigInt> readExternalProgress,
            Action<AchieveRuntime> onProgress,
            Action<AchieveRuntime> onClaimable)
        {
            this.messageId = messageId ?? string.Empty;
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

            return IsClaimable
                ? MissionRuntimeState.CLAIMABLE
                : MissionRuntimeState.ACTIVE;
        }

        public void MarkCompleted()
        {
            isCompleted = true;
        }

        public void LevelUp(
            int nextLevel,
            string nextMessageId,
            GAME_MESSAGE_TYPE nextStatType,
            GAME_MESSAGE_SAVE_TYPE nextOpType,
            CBigInt nextConditionValue,
            Func<CBigInt> readExternalProgress)
        {
            level = nextLevel;
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

            RaiseClaimableIfNeeded();
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
            if (isCompleted || _opType == GAME_MESSAGE_SAVE_TYPE.NONE || _statType != messageType)
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
}
