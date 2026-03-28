using System;
using System.Collections.Generic;
using UnityEngine;

namespace Devian
{
    public sealed class RedDotManager : CompoSingleton<RedDotManager>
    {
        readonly Dictionary<string, RedDotNode> _nodes = new(StringComparer.Ordinal);
        readonly RedDotMessageTrigger _messageTrigger = new();

        public void Set(string key, bool value)
        {
            key = normalizeKey(key);

            if (!value && !_nodes.TryGetValue(key, out _))
                return;

            var changes = new List<RedDotChanged>();
            var node = value ? getOrCreateNode(key) : _nodes[key];
            var previousIsOn = node.IsOn;
            var previousSelfOn = node.SelfOn;
            var previousHasActiveChild = node.HasActiveChild;

            if (previousSelfOn == value)
                return;

            node.SelfOn = value;
            queueStateChange(changes, node, previousIsOn, previousSelfOn, previousHasActiveChild);

            var stateDelta = boolToInt(node.IsOn) - boolToInt(previousIsOn);
            if (stateDelta != 0 && !string.IsNullOrEmpty(node.ParentKey))
                applyActiveChildDelta(node.ParentKey, stateDelta, changes);

            publishChanges(changes);
        }

        public void Clear(string key)
        {
            Set(key, false);
        }

        public void ClearAll()
        {
            if (_nodes.Count <= 0)
                return;

            var changes = new List<RedDotChanged>();
            foreach (var node in _nodes.Values)
            {
                if (!node.IsOn)
                    continue;

                changes.Add(new RedDotChanged(node.Key, false, false, false));
            }

            _nodes.Clear();
            publishChanges(changes);
        }

        public bool IsOn(string key)
        {
            return tryGetNode(key, out var node) && node.IsOn;
        }

        public bool Contains(string key)
        {
            key = normalizeKey(key);
            return _nodes.ContainsKey(key);
        }

        public bool HasActiveChild(string key)
        {
            return tryGetNode(key, out var node) && node.HasActiveChild;
        }

        public bool TryGetState(string key, out RedDotStateView state)
        {
            if (tryGetNode(key, out var node))
            {
                state = createStateView(node);
                return true;
            }

            state = default;
            return false;
        }

        public void Subcribe(EntityId ownerKey, string key, Action<RedDotChanged> handler)
        {
            if (handler == null)
                throw new ArgumentNullException(nameof(handler));

            key = normalizeKey(key);
            _messageTrigger.Subcribe(ownerKey, RED_DOT_MESSAGE_TYPE.STATE_CHANGED, args =>
            {
                if (args == null || args.Length <= 0 || args[0] is not RedDotChanged changed)
                    return false;

                if (!string.Equals(changed.Key, key, StringComparison.Ordinal))
                    return false;

                handler(changed);
                return false;
            });
        }

        public void UnSubcribe(EntityId ownerKey)
        {
            _messageTrigger.UnSubcribe(ownerKey);
        }

        void applyActiveChildDelta(string key, int delta, List<RedDotChanged> changes)
        {
            var node = getOrCreateNode(key);
            var previousIsOn = node.IsOn;
            var previousSelfOn = node.SelfOn;
            var previousHasActiveChild = node.HasActiveChild;

            node.ActiveChildCount += delta;
            if (node.ActiveChildCount < 0)
                throw new InvalidOperationException($"ActiveChildCount became negative. key={key}, delta={delta}");

            queueStateChange(changes, node, previousIsOn, previousSelfOn, previousHasActiveChild);

            var stateDelta = boolToInt(node.IsOn) - boolToInt(previousIsOn);
            if (stateDelta != 0 && !string.IsNullOrEmpty(node.ParentKey))
                applyActiveChildDelta(node.ParentKey, stateDelta, changes);
        }

        RedDotNode getOrCreateNode(string key)
        {
            if (_nodes.TryGetValue(key, out var existing))
                return existing;

            var parentKey = getParentKey(key);
            if (!string.IsNullOrEmpty(parentKey))
                getOrCreateNode(parentKey);

            var created = new RedDotNode(key, parentKey);
            _nodes[key] = created;
            return created;
        }

        bool tryGetNode(string key, out RedDotNode node)
        {
            key = normalizeKey(key);
            return _nodes.TryGetValue(key, out node);
        }

        void queueStateChange(List<RedDotChanged> changes, RedDotNode node, bool previousIsOn, bool previousSelfOn, bool previousHasActiveChild)
        {
            if (previousIsOn == node.IsOn
                && previousSelfOn == node.SelfOn
                && previousHasActiveChild == node.HasActiveChild)
            {
                return;
            }

            changes.Add(new RedDotChanged(node.Key, node.IsOn, node.SelfOn, node.HasActiveChild));
        }

        void publishChanges(List<RedDotChanged> changes)
        {
            for (var i = 0; i < changes.Count; i++)
                _messageTrigger.NotifyStateChanged(changes[i]);
        }

        static RedDotStateView createStateView(RedDotNode node)
        {
            return new RedDotStateView(node.Key, node.IsOn, node.SelfOn, node.HasActiveChild);
        }

        static int boolToInt(bool value)
        {
            return value ? 1 : 0;
        }

        static string getParentKey(string key)
        {
            var dotIndex = key.LastIndexOf('.');
            return dotIndex <= 0 ? null : key.Substring(0, dotIndex);
        }

        static string normalizeKey(string key)
        {
            if (key == null)
                throw new ArgumentNullException(nameof(key));

            key = key.Trim();
            if (key.Length <= 0)
                throw new ArgumentException("key is empty.", nameof(key));

            if (key[0] == '.' || key[key.Length - 1] == '.')
                throw new ArgumentException($"Invalid red dot key: {key}", nameof(key));

            var segmentStart = 0;
            for (var i = 0; i < key.Length; i++)
            {
                if (key[i] != '.')
                    continue;

                if (i == segmentStart)
                    throw new ArgumentException($"Invalid red dot key: {key}", nameof(key));

                segmentStart = i + 1;
            }

            if (segmentStart >= key.Length)
                throw new ArgumentException($"Invalid red dot key: {key}", nameof(key));

            return key;
        }
    }
}
