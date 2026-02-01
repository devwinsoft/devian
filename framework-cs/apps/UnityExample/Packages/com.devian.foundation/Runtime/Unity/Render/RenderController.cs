using System.Collections.Generic;
using UnityEngine;

namespace Devian
{
    /// <summary>
    /// Actor에 붙는 렌더 제어 컴포넌트.
    /// SSOT: skills/devian-unity/30-unity-components/28-render-controller/SKILL.md
    /// </summary>
    public sealed class RenderController : BaseController<GameObject>
    {
        [Tooltip("Driver component to use. If null, will search for IRenderDriver on this GameObject.")]
        [SerializeField] private Component _driverComponent;

        [Tooltip("Default effect asset. Applied when no other effects are active.")]
        [SerializeField] private RenderEffectAsset _defaultEffectAsset;

        private IRenderDriver _driver;
        private IRenderEffect _defaultEffect;

        private readonly Dictionary<int, EffectEntry> _effects = new Dictionary<int, EffectEntry>();
        private int _nextHandle = 1;
        private long _nextSequence = 0;

        private int _currentAppliedHandle = 0; // 0 = default

        private struct EffectEntry
        {
            public RenderEffectAsset Asset;
            public IRenderEffect Instance;
            public int Priority;
            public long Sequence;
        }

        private void Awake()
        {
            // 0. BaseController 바인딩
            Init(gameObject);

            // 1. driver resolve
            if (_driverComponent != null)
            {
                _driver = _driverComponent as IRenderDriver;
            }

            if (_driver == null)
            {
                _driver = GetComponent<IRenderDriver>();
            }

            if (_driver == null || !_driver.IsValid)
            {
                Debug.LogError($"[RenderController] IRenderDriver not found or invalid on {gameObject.name}");
                return;
            }

            // 2. baseline capture
            _driver.CaptureBaseline();

            // 3. default effect 준비 및 적용
            if (_defaultEffectAsset != null)
            {
                _defaultEffect = _defaultEffectAsset.Rent();
            }

            // effect 0개 상태이므로 default 적용
            _ApplySelected();
        }

        private void OnDestroy()
        {
            // 모든 effect 반환
            _ClearEffects();

            // default effect 반환
            if (_defaultEffect != null && _defaultEffectAsset != null)
            {
                _defaultEffectAsset.Return(_defaultEffect);
                _defaultEffect = null;
            }
        }

        /// <summary>
        /// Add effect from asset. Returns handle (> 0) on success, 0 on failure.
        /// </summary>
        public int _AddEffect(RenderEffectAsset asset)
        {
            if (asset == null)
            {
                Debug.LogWarning("[RenderController] _AddEffect: asset is null");
                return 0;
            }

            if (_driver == null || !_driver.IsValid)
            {
                Debug.LogWarning("[RenderController] _AddEffect: driver is invalid");
                return 0;
            }

            var instance = asset.Rent();
            if (instance == null)
            {
                Debug.LogError($"[RenderController] _AddEffect: asset.Rent() returned null for {asset.name}");
                return 0;
            }

            int handle = _nextHandle++;
            long sequence = _nextSequence++;

            _effects[handle] = new EffectEntry
            {
                Asset = asset,
                Instance = instance,
                Priority = instance.Priority,
                Sequence = sequence
            };

            _ApplySelected();
            return handle;
        }

        /// <summary>
        /// Add effect by ID. Returns handle (> 0) on success, 0 on failure.
        /// </summary>
        public int _AddEffect(RENDER_EFFECT_ID id)
        {
            if (id == null || !id.IsValid)
            {
                Debug.LogWarning("[RenderController] _AddEffect: id is null or invalid");
                return 0;
            }

            var asset = AssetManager.GetAsset<RenderEffectAsset>(id.Value);
            if (asset == null)
            {
                Debug.LogWarning($"[RenderController] _AddEffect: asset not found for id '{id.Value}'");
                return 0;
            }

            return _AddEffect(asset);
        }

        /// <summary>
        /// Remove effect by handle. Returns true on success.
        /// </summary>
        public bool _RemoveEffect(int handle)
        {
            if (!_effects.TryGetValue(handle, out var entry))
            {
                return false;
            }

            _effects.Remove(handle);

            // 인스턴스 반환
            if (entry.Asset != null && entry.Instance != null)
            {
                entry.Asset.Return(entry.Instance);
            }

            _ApplySelected();
            return true;
        }

        /// <summary>
        /// Remove all effects.
        /// </summary>
        public void _ClearEffects()
        {
            foreach (var kvp in _effects)
            {
                var entry = kvp.Value;
                if (entry.Asset != null && entry.Instance != null)
                {
                    entry.Asset.Return(entry.Instance);
                }
            }

            _effects.Clear();
            _ApplySelected();
        }

        /// <summary>
        /// 우선순위 기준으로 선택된 effect를 적용한다.
        /// priority 큰 것 우선, 동률이면 sequence 큰 것(나중에 추가된 것) 우선
        /// </summary>
        private void _ApplySelected()
        {
            if (_driver == null || !_driver.IsValid)
                return;

            // 선택 계산
            int selectedHandle = 0;
            IRenderEffect selectedEffect = null;
            int bestPriority = int.MinValue;
            long bestSequence = -1;

            foreach (var kvp in _effects)
            {
                var entry = kvp.Value;
                bool isBetter = false;

                if (entry.Priority > bestPriority)
                {
                    isBetter = true;
                }
                else if (entry.Priority == bestPriority && entry.Sequence > bestSequence)
                {
                    isBetter = true;
                }

                if (isBetter)
                {
                    selectedHandle = kvp.Key;
                    selectedEffect = entry.Instance;
                    bestPriority = entry.Priority;
                    bestSequence = entry.Sequence;
                }
            }

            // effect가 0개면 default 사용
            if (selectedEffect == null)
            {
                selectedHandle = 0;
                selectedEffect = _defaultEffect;
            }

            // 동일한 effect가 이미 적용중이면 스킵
            if (_currentAppliedHandle == selectedHandle)
                return;

            _currentAppliedHandle = selectedHandle;

            // 적용 순서: RestoreBaseline → Apply
            _driver.RestoreBaseline();

            if (selectedEffect != null)
            {
                selectedEffect.Apply(_driver);
            }
        }
    }
}
