using System;
using System.Collections.Generic;
using Devian.Domain.Common;
using Devian.Domain.Game;
using UnityEngine;

namespace Devian
{
    public sealed class GameMessageManager : CompoSingleton<GameMessageManager>
    {
        const string Tag = nameof(GameMessageManager);

        readonly struct MessageSaveBinding
        {
            public MessageSaveBinding(string messageId, GAME_MESSAGE_SAVE_TYPE saveType)
            {
                MessageId = messageId;
                SaveType = saveType;
            }

            public string MessageId { get; }
            public GAME_MESSAGE_SAVE_TYPE SaveType { get; }
        }

        readonly GameMessageStorage _storage = new();
        readonly GameMessageTrigger _gameMessageTriggerSystem = new();
        readonly Dictionary<GAME_MESSAGE_TYPE, List<MessageSaveBinding>> _saveBindingsByMessageType = new();
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

        public void Notify(GAME_MESSAGE_TYPE messageType, long value)
        {
            Notify(messageType, CBigInt.FromLong(value));
        }

        public void Notify(GAME_MESSAGE_TYPE messageType, int value)
        {
            Notify(messageType, CBigInt.FromInt(value));
        }

        public void Notify(GAME_MESSAGE_TYPE messageType, CBigInt value)
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
            GAME_MESSAGE_TYPE messageType,
            BaseTrigger<int, GAME_MESSAGE_TYPE>.Handler handler)
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

        void onBeforeTriggerNotify(GAME_MESSAGE_TYPE messageType, CBigInt delta)
        {
            if (!_saveBindingsByMessageType.TryGetValue(messageType, out var bindings) || bindings == null || bindings.Count <= 0)
                return;

            foreach (var binding in bindings)
            {
                var current = _storage.GetStat(binding.MessageId);
                CBigInt next;
                switch (binding.SaveType)
                {
                    case GAME_MESSAGE_SAVE_TYPE.TOTAL_SUM:
                        next = current + delta;
                        break;

                    case GAME_MESSAGE_SAVE_TYPE.TOTAL_MAX:
                        next = CBigInt.Max(current, delta);
                        break;

                    default:
                        continue;
                }

                if (next.CompareTo(current) == 0)
                    continue;

                _storage.SetStat(binding.MessageId, next);
            }
        }

        void rebuildSaveBindings()
        {
            _saveBindingsByMessageType.Clear();
            var messageRows = TB_MESSAGE.GetAll() ?? new List<MESSAGE>();

            foreach (var message in messageRows)
            {
                if (message == null
                    || string.IsNullOrWhiteSpace(message.MessageId)
                    || !isTotalSaveType(message.SaveType))
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

        static bool isTotalSaveType(GAME_MESSAGE_SAVE_TYPE saveType)
        {
            return saveType == GAME_MESSAGE_SAVE_TYPE.TOTAL_SUM
                   || saveType == GAME_MESSAGE_SAVE_TYPE.TOTAL_MAX;
        }
    }
}
