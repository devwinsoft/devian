using System;
using UnityEngine;

namespace Devian
{
    /// <summary>
    /// Effect prefab id (prefab.name).
    /// SSOT: skills/devian-unity/30-unity-components/22-effect-manager/SKILL.md
    /// </summary>
    [Serializable]
    public sealed class EFFECT_ID
    {
        public string Value;

        public bool IsValid => !string.IsNullOrEmpty(Value);

        public static implicit operator string(EFFECT_ID obj)
        {
            return obj == null ? string.Empty : (obj.Value ?? string.Empty);
        }

        public static implicit operator EFFECT_ID(string value)
        {
            return new EFFECT_ID { Value = value };
        }
    }
}
