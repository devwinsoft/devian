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
    /// SSOT: skills/devian-unity/30-unity-components/22-common-effect-manager/SKILL.md
    ///
    /// AutoSingleton-based: 없으면 자동 생성. 씬에 CompoSingleton으로 배치하면 우선.
    /// </summary>
    public sealed class CommonEffectManager : AutoSingleton<CommonEffectManager>
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

        public CommonEffectObject CreateEffect(
            COMMON_EFFECT_ID effectId,
            Transform attachTr,
            Vector3 offset,
            Vector3 euler,
            COMMON_EFFECT_ATTACH_TYPE attachType)
        {
            var rotation = Quaternion.Euler(euler);
            return CreateEffect(effectId, attachTr, offset, rotation, attachType);
        }

        public CommonEffectObject CreateEffect(
            COMMON_EFFECT_ID effectId,
            Transform attachTr,
            Vector3 offset,
            Quaternion rotation,
            COMMON_EFFECT_ATTACH_TYPE attachType)
        {
            if (effectId == null || !effectId.IsValid)
            {
                return null;
            }

            var parent = attachType == COMMON_EFFECT_ATTACH_TYPE.Child ? attachTr : _root;

            var inst = BundlePool.Spawn<CommonEffectObject>(
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
                case COMMON_EFFECT_ATTACH_TYPE.Ground:
                {
                    var ray = new Ray(inst.transform.position + new Vector3(0f, 100f, 0f), Vector3.down);
                    if (Physics.SphereCast(ray, 0.01f, out var hit, 1000f))
                    {
                        inst.transform.position = hit.point;
                    }
                    inst.transform.SetParent(_root, true);
                    break;
                }
                case COMMON_EFFECT_ATTACH_TYPE.World:
                {
                    inst.transform.SetParent(_root, true);
                    break;
                }
                case COMMON_EFFECT_ATTACH_TYPE.Child:
                default:
                    // keep attachTr
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
