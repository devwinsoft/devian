using UnityEngine;

namespace Devian
{
    public enum EFFECT_ATTACH_TYPE
    {
        World,
        Ground,
        Child,
    }

    /// <summary>
    /// Effect manager (auto-created singleton).
    /// Spawns EffectObject via BundlePool.
    /// SSOT: skills/devian-unity/30-unity-components/22-effect-manager/SKILL.md
    /// </summary>
    public sealed class EffectManager : AutoSingleton<EffectManager>
    {
        /// <summary>
        /// Root transform for world-attached effects.
        /// Defaults to this.transform.
        /// </summary>
        [SerializeField] private Transform _root;

        protected override void Awake()
        {
            base.Awake();
            if (_root == null) _root = transform;
        }

        public EffectObject CreateEffect(
            EFFECT_ID effectId,
            Transform attachTr,
            Vector3 offset,
            Vector3 euler,
            EFFECT_ATTACH_TYPE attachType)
        {
            var rotation = Quaternion.Euler(euler);
            return CreateEffect(effectId, attachTr, offset, rotation, attachType);
        }

        public EffectObject CreateEffect(
            EFFECT_ID effectId,
            Transform attachTr,
            Vector3 offset,
            Quaternion rotation,
            EFFECT_ATTACH_TYPE attachType)
        {
            if (effectId == null || !effectId.IsValid)
            {
                return null;
            }

            var parent = attachType == EFFECT_ATTACH_TYPE.Child ? attachTr : _root;

            var inst = BundlePool.Spawn<EffectObject>(
                effectId.Value,
                position: attachTr != null ? attachTr.position + offset : offset,
                rotation: rotation,
                parent: parent);

            if (inst == null)
            {
                return null;
            }

            // Apply attach rules
            switch (attachType)
            {
                case EFFECT_ATTACH_TYPE.Ground:
                {
                    var ray = new Ray(inst.transform.position + new Vector3(0f, 100f, 0f), Vector3.down);
                    if (Physics.SphereCast(ray, 0.01f, out var hit, 1000f))
                    {
                        inst.transform.position = hit.point;
                    }
                    inst.transform.SetParent(_root, true);
                    break;
                }
                case EFFECT_ATTACH_TYPE.World:
                {
                    inst.transform.SetParent(_root, true);
                    break;
                }
                case EFFECT_ATTACH_TYPE.Child:
                default:
                    // keep attachTr
                    break;
            }

            inst.Init();
            return inst;
        }

        public void Remove(EffectObject effect)
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
