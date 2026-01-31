// SSOT: skills/devian-unity/30-unity-components/27-bootstrap-resource-object/SKILL.md

#nullable enable

using UnityEngine;

namespace Devian
{
    /// <summary>
    /// Resources BootstrapRoot prefab의 루트 컴포넌트.
    /// DevianSettings는 Resources로 옮기지 않고, 이 Prefab이 참조로 보유한다.
    /// </summary>
    public sealed class DevianBootstrapRoot : MonoBehaviour
    {
        [SerializeField] private DevianSettings? _settings;

        public DevianSettings? Settings => _settings;

        public void SetSettings(DevianSettings? settings)
        {
            _settings = settings;
        }
    }
}
