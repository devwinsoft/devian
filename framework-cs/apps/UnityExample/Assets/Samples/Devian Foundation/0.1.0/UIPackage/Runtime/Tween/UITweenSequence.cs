using System.Collections.Generic;

namespace Devian
{
    public sealed class UITweenSequence
    {
        private readonly List<List<UITransitionPreset>> _groups = new List<List<UITransitionPreset>>();

        public bool IsEmpty => _groups.Count == 0;

        public UITweenSequence Append(UITransitionPreset preset)
        {
            if (preset == null)
            {
                return this;
            }

            _groups.Add(new List<UITransitionPreset> { preset });
            return this;
        }

        public UITweenSequence Append(UITransitionPresetAsset asset)
        {
            return Append(asset == null ? null : asset.Preset);
        }

        public UITweenSequence Join(UITransitionPreset preset)
        {
            if (preset == null)
            {
                return this;
            }

            if (_groups.Count == 0)
            {
                _groups.Add(new List<UITransitionPreset>());
            }

            _groups[_groups.Count - 1].Add(preset);
            return this;
        }

        public UITweenSequence Join(UITransitionPresetAsset asset)
        {
            return Join(asset == null ? null : asset.Preset);
        }

        internal int GroupCount => _groups.Count;

        internal IReadOnlyList<UITransitionPreset> GetGroup(int index)
        {
            return _groups[index];
        }
    }
}
