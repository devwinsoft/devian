using System;
using System.Collections.Generic;
using Devian.Domain.Common;
using Devian.Domain.Game;
using UnityEngine;

namespace Devian
{
    public sealed class MetaMessageManager : CompoSingleton<MetaMessageManager>
    {
        const string Tag = nameof(MetaMessageManager);

        readonly struct MessageSaveBinding
        {
            public MessageSaveBinding(string messageId, MESSAGE_META_SAVE_TYPE saveType)
            {
                MessageId = messageId;
                SaveType = saveType;
            }

            public string MessageId { get; }
            public MESSAGE_META_SAVE_TYPE SaveType { get; }
        }

        readonly GameMessageStorage _storage = new();
        readonly GameMessageTrigger _gameMessageTriggerSystem = new();
        readonly Dictionary<MESSAGE_META_TYPE, List<MessageSaveBinding>> _saveBindingsByMessageType = new();
        bool _initialized;

        public GameMessageStorage Storage => _storage;
        public bool IsInitialized => _initialized;

        protected override void onInitAwake()
        {
        }

        public CommonResult Initialize()
        {
            rebuildSaveBindings();
            _initialized = true;
            return CommonResult.Ok();
        }

        public bool TryGetStat(string messageId, out CBigInt value)
        {
            return _storage.TryGetStat(messageId, out value);
        }

        public CBigInt GetStat(string messageId)
        {
            return _storage.GetStat(messageId);
        }

        public void SetStat(string messageId, CBigInt value)
        {
            _storage.SetStat(messageId, value);
        }

        public void ClearStorage()
        {
            _storage.Clear();
        }

        public void Notify(MESSAGE_META_TYPE messageType, long value)
        {
            Notify(messageType, CBigInt.FromLong(value));
        }

        public void Notify(MESSAGE_META_TYPE messageType, int value)
        {
            Notify(messageType, CBigInt.FromInt(value));
        }

        public void Notify(MESSAGE_META_TYPE messageType, CBigInt value)
        {
            if (!_initialized)
            {
                Debug.LogError($"[{Tag}] Initialize() must be called before Notify(). messageType={messageType}");
                return;
            }

            onBeforeTriggerNotify(messageType, value);
            _gameMessageTriggerSystem.Notify(messageType, value);
        }

        internal void SubcribeGameMessageTrigger(
            int ownerKey,
            MESSAGE_META_TYPE messageType,
            BaseTrigger<int, MESSAGE_META_TYPE>.Handler handler)
        {
            _gameMessageTriggerSystem.Subcribe(ownerKey, messageType, handler);
        }

        internal void UnSubcribeGameMessageTrigger(int ownerKey)
        {
            _gameMessageTriggerSystem.UnSubcribe(ownerKey);
        }

        public void ClearAll()
        {
            ClearStorage();
            _gameMessageTriggerSystem.ClearAll();
            _initialized = false;
        }

        void onBeforeTriggerNotify(MESSAGE_META_TYPE messageType, CBigInt delta)
        {
            if (!tryGetSaveBindings(messageType, out var bindings))
            {
                tryRebuildSaveBindingsFor(messageType);
                if (!tryGetSaveBindings(messageType, out bindings))
                    return;
            }

            foreach (var binding in bindings)
            {
                var hasCurrent = _storage.TryGetStat(binding.MessageId, out var rawCurrent);
                var current = GameMessageRule.ClampNonNegative(rawCurrent);
                var normalizedCurrentChanged = hasCurrent && current.CompareTo(rawCurrent) != 0;
                CBigInt next;
                switch (binding.SaveType)
                {
                    case MESSAGE_META_SAVE_TYPE.TOTAL_SUM:
                        next = GameMessageRule.ClampNonNegative(current + delta);
                        break;

                    case MESSAGE_META_SAVE_TYPE.TOTAL_MAX:
                        if (delta.CompareTo(CBigInt.Zero) < 0)
                            continue;
                        next = CBigInt.Max(current, delta);
                        break;

                    case MESSAGE_META_SAVE_TYPE.TOTAL_MIN:
                        if (delta.CompareTo(CBigInt.Zero) < 0)
                            continue;

                        next = hasCurrent
                            ? CBigInt.Min(current, delta)
                            : delta;
                        break;

                    default:
                        continue;
                }

                next = GameMessageRule.ClampNonNegative(next);
                if (!normalizedCurrentChanged && hasCurrent && next.CompareTo(current) == 0)
                    continue;

                _storage.SetStat(binding.MessageId, next);
            }
        }

        bool tryGetSaveBindings(MESSAGE_META_TYPE messageType, out List<MessageSaveBinding> bindings)
        {
            if (_saveBindingsByMessageType.TryGetValue(messageType, out bindings)
                && bindings != null
                && bindings.Count > 0)
            {
                return true;
            }

            bindings = null;
            return false;
        }

        void tryRebuildSaveBindingsFor(MESSAGE_META_TYPE messageType)
        {
            var messageRows = TB_MESSAGE_META.GetAll();
            if (messageRows == null || messageRows.Count <= 0)
                return;

            foreach (var message in messageRows)
            {
                if (message == null
                    || string.IsNullOrWhiteSpace(message.MessageId)
                    || message.MessageType != messageType
                    || !GameMessageRule.IsTotalSaveType(message.SaveType))
                {
                    continue;
                }

                rebuildSaveBindings();
                return;
            }
        }

        void rebuildSaveBindings()
        {
            _saveBindingsByMessageType.Clear();
            var messageRows = TB_MESSAGE_META.GetAll() ?? new List<MESSAGE_META>();

            foreach (var message in messageRows)
            {
                if (message == null
                    || string.IsNullOrWhiteSpace(message.MessageId)
                    || !GameMessageRule.IsTotalSaveType(message.SaveType))
                {
                    continue;
                }

                if (!_saveBindingsByMessageType.TryGetValue(message.MessageType, out var bindings))
                {
                    bindings = new List<MessageSaveBinding>();
                    _saveBindingsByMessageType[message.MessageType] = bindings;
                }

                bindings.Add(new MessageSaveBinding(message.MessageId, message.SaveType));
            }
        }
    }

    internal static class GameMessageRule
    {
        public static bool IsTotalSaveType(MESSAGE_META_SAVE_TYPE saveType)
        {
            return saveType == MESSAGE_META_SAVE_TYPE.TOTAL_SUM
                   || saveType == MESSAGE_META_SAVE_TYPE.TOTAL_MAX
                   || saveType == MESSAGE_META_SAVE_TYPE.TOTAL_MIN;
        }

        public static CBigInt ClampNonNegative(CBigInt value)
        {
            return value.CompareTo(CBigInt.Zero) < 0
                ? CBigInt.Zero
                : value;
        }

        public static bool IsConditionSatisfied(CBigInt progress, MESSAGE_META_OP_TYPE opType, CBigInt conditionValue)
        {
            var compare = progress.CompareTo(conditionValue);
            switch (opType)
            {
                case MESSAGE_META_OP_TYPE.EQ:
                    return compare == 0;

                case MESSAGE_META_OP_TYPE.LTE:
                    return compare <= 0;

                case MESSAGE_META_OP_TYPE.GTE:
                case MESSAGE_META_OP_TYPE.NONE:
                default:
                    return compare >= 0;
            }
        }
    }
}
