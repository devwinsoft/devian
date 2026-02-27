using UnityEngine;

namespace Devian
{
    public enum COMMON_EFFECT_ATTACH_TYPE
    {
        World,
        Ground,
        Child,
    }

    /// <summary>
    /// Common effect manager.
    /// Spawns CommonEffectObject via BundlePool.
    /// SSOT: skills/devian-unity/11-common-system/21-common-effect-manager/SKILL.md
    ///
    /// AutoSingleton-based: 없으면 자동 생성. 씬에 CompoSingleton으로 배치하면 우선.
    /// </summary>
    public sealed class CommonEffectManager : AutoSingleton<CommonEffectManager>
    {
        public CommonEffectObject CreateEffect(
            COMMON_EFFECT_ID effectId,
            Transform attachTr,
            Vector3 offset,
            Vector3 euler,
            COMMON_EFFECT_ATTACH_TYPE attachType,
            Transform parent = null)
        {
            var rotation = Quaternion.Euler(euler);
            return CreateEffect(effectId, attachTr, offset, rotation, attachType, parent);
        }

        public CommonEffectObject CreateEffect(
            COMMON_EFFECT_ID effectId,
            Transform attachTr,
            Vector3 offset,
            Quaternion rotation,
            COMMON_EFFECT_ATTACH_TYPE attachType,
            Transform parent = null)
        {
            if (effectId == null || !effectId.IsValid)
            {
                return null;
            }

            var spawnParent = parent;
            if (spawnParent == null && attachType == COMMON_EFFECT_ATTACH_TYPE.Child)
            {
                spawnParent = attachTr;
            }

            var inst = BundlePool.Spawn<CommonEffectObject>(
                effectId.Value,
                position: attachTr != null ? attachTr.position + offset : offset,
                rotation: rotation,
                parent: spawnParent);

            if (inst == null)
            {
                return null;
            }

            // Apply attach rules
            switch (attachType)
            {
                case COMMON_EFFECT_ATTACH_TYPE.Ground:
                {
                    var ray = new Ray(inst.transform.position + new Vector3(0f, 100f, 0f), Vector3.down);
                    if (Physics.SphereCast(ray, 0.01f, out var hit, 1000f))
                    {
                        inst.transform.position = hit.point;
                    }
                    break;
                }
                case COMMON_EFFECT_ATTACH_TYPE.World:
                case COMMON_EFFECT_ATTACH_TYPE.Child:
                default:
                    // Keep the spawn parent resolved above.
                    break;
            }

            inst.Init();
            return inst;
        }

        public void Remove(CommonEffectObject effect)
        {
            if (effect == null) return;
            effect.Remove();
        }

        /// <summary>
        /// Global clear (clears ALL pools). Use carefully.
        /// </summary>
        public void ClearAllPools()
        {
            BundlePool.ClearAll();
        }
    }
}
