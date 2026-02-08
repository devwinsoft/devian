using System.Collections.Generic;
using UnityEngine;

namespace Devian
{
    /// <summary>
    /// 단일 Renderer를 위한 BaseMaterialEffectDriver 구현체 (v2).
    /// Material[] 교체만 지원하며, PropertyBlock/Common 기능은 제거됨.
    /// </summary>
    public sealed class MaterialEffectDriverCommon : BaseMaterialEffectDriver
    {
        [Tooltip("Target renderer. If null, will auto-acquire from this GameObject (no children search).")]
        [SerializeField] private Renderer _renderer;

        [Tooltip("Clone materials on apply to prevent shared material modification.")]
        [SerializeField] private bool _cloneOnApply = true;

        private const string ClonePrefix = "__DevianClone__";

        // baseline (원본 참조 저장, clone 생성 금지)
        private Material[] _baselineMaterials;
        private bool _baselineCaptured;

        // clone-on-apply
        private readonly Dictionary<int, Material> _applyCloneCache = new Dictionary<int, Material>();
        private readonly List<Material> _applyClones = new List<Material>();
        private readonly HashSet<int> _applyCloneIds = new HashSet<int>();

        public override bool IsValid => _renderer != null;

        private void Awake()
        {
            EnsureRenderer();
        }

        private void OnDisable()
        {
            RestoreBaseline();
        }

        private void OnDestroy()
        {
            DisposeBaseline();
        }

        /// <summary>
        /// Renderer가 null이면 같은 GO에서 획득한다 (자식 탐색 금지: 1:1 정책).
        /// </summary>
        private void EnsureRenderer()
        {
            if (_renderer == null)
            {
                _renderer = GetComponent<Renderer>();
            }
        }

        /// <summary>
        /// 이미 Devian이 만든 clone인지 판별한다. true면 재-clone 금지.
        /// </summary>
        private bool IsDevianClone(Material m)
        {
            if (m == null)
                return false;

            if (_applyCloneIds.Contains(m.GetInstanceID()))
                return true;

            if (m.name != null && m.name.StartsWith(ClonePrefix))
                return true;

            return false;
        }

        /// <summary>
        /// apply용 clone을 가져오거나 생성한다. 캐시를 재사용하고, 이미 clone이면 그대로 반환.
        /// </summary>
        private Material GetOrCreateApplyClone(Material source)
        {
            if (source == null)
                return null;

            if (!_cloneOnApply)
                return source;

            // 이미 clone이면 재-clone 금지
            if (IsDevianClone(source))
                return source;

            int srcId = source.GetInstanceID();

            // 캐시에 있으면 재사용
            if (_applyCloneCache.TryGetValue(srcId, out var existing) && existing != null)
                return existing;

            // 새 clone 생성
            var clone = new Material(source);
            clone.hideFlags = HideFlags.DontSave;
            clone.name = ClonePrefix + source.name;

            _applyCloneCache[srcId] = clone;
            _applyCloneIds.Add(clone.GetInstanceID());
            _applyClones.Add(clone);

            return clone;
        }

        /// <summary>
        /// apply로 만든 clone들을 전부 정리한다 (effect 전환 시 누수 방지).
        /// </summary>
        private void DisposeAppliedClones()
        {
            foreach (var mat in _applyClones)
            {
                if (mat != null)
                {
                    Object.Destroy(mat);
                }
            }

            _applyClones.Clear();
            _applyCloneCache.Clear();
            _applyCloneIds.Clear();
        }

        public override void CaptureBaseline()
        {
            // Renderer 자동 획득 보장 (Awake 순서가 뒤집혀도 안전)
            EnsureRenderer();

            if (_renderer == null)
                return;

            // 기존 baseline이 있으면 먼저 정리
            if (_baselineCaptured)
            {
                DisposeBaseline();
            }

            // 원본 참조 저장 (clone 금지 — 효과 0개일 때 __DevianClone__ 잔존 방지)
            var mats = _renderer.sharedMaterials;
            _baselineMaterials = (mats != null && mats.Length > 0) ? mats : null;
            _baselineCaptured = true;
        }

        public override void RestoreBaseline()
        {
            // apply clone을 먼저 정리 (effect 전환 시 누수 방지)
            DisposeAppliedClones();

            if (!_baselineCaptured || _baselineMaterials == null || _renderer == null)
                return;

            // PB clear: sharedMaterials 변경 전에 PropertyBlock 제거
            _renderer.SetPropertyBlock(null);

            // 원본 baseline을 Renderer에 재적용
            _renderer.sharedMaterials = _baselineMaterials;
        }

        public override void DisposeBaseline()
        {
            // apply clone 정리 (OnDestroy 경로에서 누수 방지)
            DisposeAppliedClones();

            // baseline은 원본 참조이므로 Destroy 금지 — 참조만 해제
            _baselineMaterials = null;
            _baselineCaptured = false;
        }

        public override void SetSharedMaterials(Material[] materials)
        {
            if (_renderer == null)
                return;

            // PB clear: sharedMaterials 변경 전에 PropertyBlock 제거
            _renderer.SetPropertyBlock(null);

            if (materials == null)
            {
                _renderer.sharedMaterials = null;
                return;
            }

            if (!_cloneOnApply)
            {
                // clone 없이 그대로 적용
                _renderer.sharedMaterials = materials;
                return;
            }

            // clone-on-apply: 각 Material을 clone하여 적용
            var cloned = new Material[materials.Length];
            for (int i = 0; i < materials.Length; i++)
            {
                cloned[i] = GetOrCreateApplyClone(materials[i]);
            }

            _renderer.sharedMaterials = cloned;
        }

        public override void SetVisible(bool visible)
        {
            if (_renderer != null)
            {
                _renderer.enabled = visible;
            }
        }
    }
}
