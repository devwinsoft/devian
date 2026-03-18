using System;
using Devian.Domain.Game;

namespace Devian
{
    [Serializable]
    public abstract class AchieveRuntimeBase
    {
        public string achieveId = string.Empty;
        public int achieveUid;
        public int level = 1;
        public int index;
        public CBigInt progressValue = CBigInt.Zero;
        public MissionRuntimeState state = MissionRuntimeState.ACTIVE;

        [NonSerialized] private GAME_MESSAGE_TYPE _statType;
        [NonSerialized] private GAME_MESSAGE_SAVE_TYPE _opType;
        [NonSerialized] private GAME_MESSAGE_OP_TYPE _conditionOpType = GAME_MESSAGE_OP_TYPE.GTE;
        [NonSerialized] private CBigInt _conditionValue = CBigInt.Zero;
        [NonSerialized] private bool _hasSessionMinSample;
        [NonSerialized] private Action<AchieveRuntimeBase> _onProgress;
        [NonSerialized] private Action<AchieveRuntimeBase> _onClaimable;
        [NonSerialized] private Func<CBigInt> _externalProgressReader;

        public abstract ACHIEVE_TYPE RuntimeType { get; }

        public GAME_MESSAGE_TYPE StatType => _statType;
        public GAME_MESSAGE_SAVE_TYPE OpType => _opType;
        public GAME_MESSAGE_OP_TYPE ConditionOpType => _conditionOpType;
        public CBigInt ConditionValue => _conditionValue;
        public bool IsClaimable => state == MissionRuntimeState.ACTIVE
                                   && GameMessageRule.IsConditionSatisfied(
                                       progressValue,
                                       _conditionOpType,
                                       _conditionValue);
        public int Index => index;

        internal void Bind(
            GAME_MESSAGE_TYPE statType,
            GAME_MESSAGE_SAVE_TYPE opType,
            GAME_MESSAGE_OP_TYPE conditionOpType,
            CBigInt conditionValue,
            Func<CBigInt> readExternalProgress,
            Action<AchieveRuntimeBase> onProgress,
            Action<AchieveRuntimeBase> onClaimable)
        {
            _statType = statType;
            _opType = opType;
            _conditionOpType = conditionOpType;
            _conditionValue = conditionValue;
            _hasSessionMinSample = opType == GAME_MESSAGE_SAVE_TYPE.SESSION_MIN
                                   && progressValue.CompareTo(CBigInt.Zero) != 0;
            _externalProgressReader = readExternalProgress;
            _onProgress = onProgress;
            _onClaimable = onClaimable;

            RefreshProgressFromExternal(emitProgressEvent: false);

            if (IsClaimable)
                _onClaimable?.Invoke(this);
        }

        internal void BindWaiting(
            Action<AchieveRuntimeBase> onProgress,
            Action<AchieveRuntimeBase> onClaimable)
        {
            _statType = GAME_MESSAGE_TYPE.NONE;
            _opType = GAME_MESSAGE_SAVE_TYPE.NONE;
            _conditionOpType = GAME_MESSAGE_OP_TYPE.GTE;
            _conditionValue = CBigInt.Zero;
            _hasSessionMinSample = false;
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
            if (state == MissionRuntimeState.ACTIVE && IsClaimable)
                return MissionRuntimeState.CLAIMABLE;

            return state;
        }

        public void MarkCompleted()
        {
            state = MissionRuntimeState.COMPLETED;
        }

        public void LevelUp(
            int nextLevel,
            int nextIndex,
            GAME_MESSAGE_TYPE nextStatType,
            GAME_MESSAGE_SAVE_TYPE nextOpType,
            GAME_MESSAGE_OP_TYPE nextConditionOpType,
            CBigInt nextConditionValue,
            Func<CBigInt> readExternalProgress)
        {
            level = nextLevel;
            index = nextIndex;
            state = MissionRuntimeState.ACTIVE;
            _statType = nextStatType;
            _opType = nextOpType;
            _conditionOpType = nextConditionOpType;
            _conditionValue = nextConditionValue;
            _hasSessionMinSample = nextOpType == GAME_MESSAGE_SAVE_TYPE.SESSION_MIN
                                   && progressValue.CompareTo(CBigInt.Zero) != 0;
            _externalProgressReader = readExternalProgress;

            if (nextOpType == GAME_MESSAGE_SAVE_TYPE.SESSION_SUM)
                progressValue = CBigInt.Zero;
            else if (nextOpType == GAME_MESSAGE_SAVE_TYPE.SESSION_MIN)
            {
                progressValue = CBigInt.Zero;
                _hasSessionMinSample = false;
            }
            else if (isTotalSaveType(nextOpType))
                RefreshProgressFromExternal(emitProgressEvent: false);
            else if (nextOpType == GAME_MESSAGE_SAVE_TYPE.NONE && nextConditionValue.CompareTo(CBigInt.Zero) <= 0)
                progressValue = CBigInt.Zero;

            RaiseClaimableIfNeeded();
        }

        public void LevelUpToWaiting(int nextLevel, int nextIndex)
        {
            level = nextLevel;
            index = nextIndex;
            state = MissionRuntimeState.WAIT;
            _statType = GAME_MESSAGE_TYPE.NONE;
            _opType = GAME_MESSAGE_SAVE_TYPE.NONE;
            _conditionOpType = GAME_MESSAGE_OP_TYPE.GTE;
            _conditionValue = CBigInt.Zero;
            _hasSessionMinSample = false;
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

        internal void OnMessageStatUpdated(GAME_MESSAGE_TYPE messageType, CBigInt delta)
        {
            if (state != MissionRuntimeState.ACTIVE || _opType == GAME_MESSAGE_SAVE_TYPE.NONE || _statType != messageType)
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
                    nextProgress = GameMessageRule.ClampNonNegative(progressValue + delta);
                    break;

                case GAME_MESSAGE_SAVE_TYPE.SESSION_MAX:
                    if (delta.CompareTo(CBigInt.Zero) < 0)
                        return;
                    nextProgress = CBigInt.Max(progressValue, delta);
                    break;

                case GAME_MESSAGE_SAVE_TYPE.SESSION_MIN:
                    if (delta.CompareTo(CBigInt.Zero) < 0)
                        return;

                    if (!_hasSessionMinSample)
                    {
                        nextProgress = delta;
                        _hasSessionMinSample = true;
                    }
                    else
                    {
                        nextProgress = CBigInt.Min(progressValue, delta);
                    }
                    break;

                default:
                    return;
            }

            nextProgress = GameMessageRule.ClampNonNegative(nextProgress);
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
                   || saveType == GAME_MESSAGE_SAVE_TYPE.TOTAL_MAX
                   || saveType == GAME_MESSAGE_SAVE_TYPE.TOTAL_MIN;
        }
    }

    [Serializable]
    public sealed class AchieveRuntimeSocial : AchieveRuntimeBase
    {
        public override ACHIEVE_TYPE RuntimeType => ACHIEVE_TYPE.SOCIAL;
    }

    [Serializable]
    public sealed class AchieveRuntimePass : AchieveRuntimeBase
    {
        public override ACHIEVE_TYPE RuntimeType => ACHIEVE_TYPE.PASS;
    }
}
