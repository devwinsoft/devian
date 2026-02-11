using System;
using UnityEngine;

namespace Devian
{
    /// <summary>
    /// Common Effect prefab id (prefab.name).
    /// SSOT: skills/devian-unity/14-effect-system/22-common-effect-manager/SKILL.md
    /// </summary>
    [Serializable]
    public sealed class COMMON_EFFECT_ID
    {
        public string Value;

        public bool IsValid => !string.IsNullOrEmpty(Value);

        public static implicit operator string(COMMON_EFFECT_ID obj)
        {
            return obj == null ? string.Empty : (obj.Value ?? string.Empty);
        }

        public static implicit operator COMMON_EFFECT_ID(string value)
        {
            return new COMMON_EFFECT_ID { Value = value };
        }
    }
}
