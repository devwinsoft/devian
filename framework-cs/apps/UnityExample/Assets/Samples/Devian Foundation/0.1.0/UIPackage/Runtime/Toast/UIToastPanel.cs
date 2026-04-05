using System.Collections.Generic;
using UnityEngine;

namespace Devian
{
    public sealed class UIToastPanel : UIBasePanel<UIToastCanvas>
    {
        private readonly Dictionary<string, UIToastGroup> _groups = new Dictionary<string, UIToastGroup>();

        protected override void onInitComplete()
        {
            EnsureGroups();
        }

        protected override void onDestroy()
        {
            foreach (var pair in _groups)
            {
                pair.Value.Clear();
            }

            _groups.Clear();
        }

        public void Enqueue(string message, string groupId)
        {
            EnsureGroups();
            ResolveGroup(groupId).Enqueue(message);
        }

        private void EnsureGroups()
        {
            if (_groups.Count > 0)
            {
                return;
            }

            var parent = rectTransform;
            var service = UIToastService.Instance;
            var groupConfigs = service != null
                ? service.GetGroupConfigs()
                : null;

            if (groupConfigs == null || groupConfigs.Length == 0)
            {
                RegisterGroup(CreateDefaultConfig(), parent);
                return;
            }

            for (var i = 0; i < groupConfigs.Length; i++)
            {
                var config = groupConfigs[i];
                if (config == null)
                {
                    continue;
                }

                var key = NormalizeGroupId(config.GroupId);
                if (_groups.ContainsKey(key))
                {
                    Debug.LogWarning($"[UIToastPanel] Duplicate toast group id '{key}' ignored.", this);
                    continue;
                }

                RegisterGroup(config, parent);
            }

            if (_groups.Count == 0)
            {
                RegisterGroup(CreateDefaultConfig(), parent);
            }
        }

        private UIToastGroup ResolveGroup(string groupId)
        {
            var key = NormalizeGroupId(groupId);
            if (_groups.TryGetValue(key, out var group))
            {
                return group;
            }

            if (_groups.TryGetValue(UIToastDefaults.DefaultGroupId, out var defaultGroup))
            {
                return defaultGroup;
            }

            return RegisterGroup(CreateDefaultConfig(), rectTransform);
        }

        private UIToastGroup RegisterGroup(ToastGroupConfig config, RectTransform parent)
        {
            var key = NormalizeGroupId(config.GroupId);
            var root = CreateGroupRoot(key, parent);
            var group = new UIToastGroup(ownerCanvas, root, config.ToastFrameId, config);
            _groups[key] = group;
            return group;
        }

        private RectTransform CreateGroupRoot(string groupId, RectTransform parent)
        {
            var go = new GameObject($"[ToastGroup] {groupId}", typeof(RectTransform));
            var root = (RectTransform)go.transform;
            root.SetParent(parent, false);
            root.localScale = Vector3.one;
            return root;
        }

        private static ToastGroupConfig CreateDefaultConfig()
        {
            return new ToastGroupConfig();
        }

        private static string NormalizeGroupId(string groupId)
        {
            if (string.IsNullOrWhiteSpace(groupId))
            {
                return UIToastDefaults.DefaultGroupId;
            }

            if (string.Equals(groupId, "Default", System.StringComparison.OrdinalIgnoreCase))
            {
                return UIToastDefaults.DefaultGroupId;
            }

            return groupId;
        }
    }
}
