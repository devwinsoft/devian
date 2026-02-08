using System.Collections.Generic;
using UnityEngine;

namespace Devian
{
    /// <summary>
    /// 단일 Renderer의 Material[] 스위치 전용 컴포넌트 (v2).
    /// Controller ↔ Driver 1:1 매칭. PropertyBlock/Common 기능 제거됨.
    /// SSOT: skills/devian-unity/30-unity-components/28-render-controller/SKILL.md
    /// </summary>
    public sealed class MaterialEffectController : BaseController<GameObject>
    {
        [Tooltip("Driver component to use. If null, will search for BaseMaterialEffectDriver on this GameObject.")]
        [SerializeField] private BaseMaterialEffectDriver _driverComponent;

        private readonly Dictionary<int, EffectEntry> _effects = new Dictionary<int, EffectEntry>();
        private int _nextHandle = 1;
        private long _nextSequence = 0;

        private int _currentAppliedHandle = -1; // -1 = not applied yet, forces first apply

#if UNITY_EDITOR
        // Editor Preview state (NonSerialized)
        [System.NonSerialized] private Material[] _editorBaselineSharedMaterials;
        [System.NonSerialized] private bool _editorPreviewActive;
#endif

        private struct EffectEntry
        {
            public MaterialEffectAsset Asset;
            public IMaterialEffect Instance;
            public int Priority;
            public long Sequence;
        }

        private void Awake()
        {
            // 0. BaseController 바인딩
            Init(gameObject);

            // 1. driver resolve
            if (_driverComponent == null)
            {
                _driverComponent = GetComponent<BaseMaterialEffectDriver>();
            }

            if (_driverComponent == null || !_driverComponent.IsValid)
            {
                Debug.LogError($"[MaterialEffectController] BaseMaterialEffectDriver not found or invalid on {gameObject.name}");
                return;
            }

            // 2. baseline capture
            _driverComponent.CaptureBaseline();

            // 3. effect 0개 상태이므로 default(handle 0)로 진입
            _ApplySelected();
        }

        private void OnDestroy()
        {
            // effect 반환만 하고 _ApplySelected()는 호출하지 않는다
            foreach (var kvp in _effects)
            {
                var entry = kvp.Value;
                if (entry.Asset != null && entry.Instance != null)
                {
                    entry.Asset.Return(entry.Instance);
                }
            }

            _effects.Clear();
        }

        /// <summary>
        /// Add effect from asset. Returns handle (> 0) on success, 0 on failure.
        /// </summary>
        public int AddEffect(MaterialEffectAsset asset)
        {
            if (asset == null)
            {
                Debug.LogWarning("[MaterialEffectController] AddEffect: asset is null");
                return 0;
            }

            if (_driverComponent == null || !_driverComponent.IsValid)
            {
                Debug.LogWarning("[MaterialEffectController] AddEffect: driver is invalid");
                return 0;
            }

            var instance = asset.Rent();
            if (instance == null)
            {
                Debug.LogError($"[MaterialEffectController] AddEffect: asset.Rent() returned null for {asset.name}");
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
        public int AddEffect(MATERIAL_EFFECT_ID id)
        {
            if (id == null || !id.IsValid)
            {
                Debug.LogWarning("[MaterialEffectController] AddEffect: id is null or invalid");
                return 0;
            }

            var asset = AssetManager.GetAsset<MaterialEffectAsset>(id.Value);
            if (asset == null)
            {
                Debug.LogWarning($"[MaterialEffectController] AddEffect: asset not found for id '{id.Value}'");
                return 0;
            }

            return AddEffect(asset);
        }

        /// <summary>
        /// Remove effect by handle. Returns true on success.
        /// </summary>
        public bool RemoveEffect(int handle)
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
        /// Get current applied effect handle. 0 = default(baseline), -1 = none (driver invalid or not applied).
        /// </summary>
        public int _GetCurrentAppliedHandle()
        {
            return _currentAppliedHandle;
        }

        /// <summary>
        /// Get current applied effect name for debugging.
        /// Returns "none" if nothing applied, "default(baseline)" if baseline is applied, otherwise asset name.
        /// </summary>
        public string _GetCurrentAppliedEffectName()
        {
            if (_currentAppliedHandle == -1)
                return "none";

            if (_currentAppliedHandle == 0)
                return "default(baseline)";

            if (_effects.TryGetValue(_currentAppliedHandle, out var entry))
            {
                return entry.Asset != null ? entry.Asset.name : "unknown";
            }

            return "none";
        }

        /// <summary>
        /// 우선순위 기준으로 선택된 effect를 적용한다.
        /// priority 큰 것 우선, 동률이면 sequence 큰 것(나중에 추가된 것) 우선
        /// effect가 0개면 default(handle 0) = baseline 복원 상태
        /// </summary>
        private void _ApplySelected()
        {
            if (_driverComponent == null || !_driverComponent.IsValid)
                return;

            // 선택 계산
            int selectedHandle = 0;
            IMaterialEffect selectedEffect = null;
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

            // effect가 0개면 default(handle 0) 사용
            if (selectedEffect == null)
            {
                selectedHandle = 0;
                // selectedEffect는 null 유지 (baseline만 복원, 추가 Apply 없음)
            }

            // 동일한 effect가 이미 적용중이면 스킵
            if (_currentAppliedHandle == selectedHandle)
                return;

            _currentAppliedHandle = selectedHandle;

            // 적용 순서: RestoreBaseline 항상 실행
            _driverComponent.RestoreBaseline();

            // effect가 있을 때만 Apply 실행
            if (selectedEffect != null)
            {
                selectedEffect.Apply(_driverComponent);
            }
            // effect가 없으면(handle 0) baseline 복원 상태가 default
        }

#if UNITY_EDITOR
        /// <summary>
        /// Editor-only: Check if preview is currently active.
        /// </summary>
        public bool EditorPreviewIsActive => _editorPreviewActive;

        /// <summary>
        /// Editor-only: Get the Renderer for this controller (same GO).
        /// </summary>
        private Renderer _EditorGetRenderer()
        {
            return GetComponent<Renderer>();
        }

        /// <summary>
        /// Editor-only: Turn on preview with specified effect.
        /// Only works in Edit Mode (not during Play).
        /// </summary>
        public void EditorPreviewOn(MaterialEffectAsset effect)
        {
            if (Application.isPlaying)
            {
                Debug.LogWarning("[MaterialEffectController] _EditorPreviewOn: Cannot preview during Play Mode.");
                return;
            }

            if (effect == null)
            {
                Debug.LogWarning("[MaterialEffectController] _EditorPreviewOn: effect is null.");
                return;
            }

            var renderer = _EditorGetRenderer();
            if (renderer == null)
            {
                Debug.LogError("[MaterialEffectController] _EditorPreviewOn: No Renderer found on this GameObject.");
                return;
            }

            // Save baseline if not already in preview
            if (!_editorPreviewActive)
            {
                _editorBaselineSharedMaterials = renderer.sharedMaterials;
            }

            // Get materials from effect
            Material[] previewMaterials = null;
            if (effect is MaterialSetMaterialEffectAsset setAsset)
            {
                previewMaterials = setAsset.Materials;
            }
            else
            {
                Debug.LogWarning($"[MaterialEffectController] _EditorPreviewOn: Effect type '{effect.GetType().Name}' does not support direct material preview.");
                return;
            }

            if (previewMaterials == null || previewMaterials.Length == 0)
            {
                Debug.LogWarning("[MaterialEffectController] _EditorPreviewOn: Effect has no materials.");
                return;
            }

            // Clear PropertyBlock and apply preview materials
            renderer.SetPropertyBlock(null);
            renderer.sharedMaterials = previewMaterials;

            _editorPreviewActive = true;
            Debug.Log($"[MaterialEffectController] Preview ON: {effect.name}");
        }

        /// <summary>
        /// Editor-only: Turn off preview and restore baseline.
        /// Only works in Edit Mode (not during Play).
        /// </summary>
        public void EditorPreviewOff()
        {
            if (Application.isPlaying)
            {
                // Silently skip during Play Mode
                return;
            }

            if (!_editorPreviewActive)
            {
                return;
            }

            var renderer = _EditorGetRenderer();
            if (renderer == null)
            {
                Debug.LogError("[MaterialEffectController] _EditorPreviewOff: No Renderer found on this GameObject.");
                _editorPreviewActive = false;
                return;
            }

            // Clear PropertyBlock and restore baseline
            renderer.SetPropertyBlock(null);

            if (_editorBaselineSharedMaterials != null)
            {
                renderer.sharedMaterials = _editorBaselineSharedMaterials;
            }

            _editorPreviewActive = false;
            _editorBaselineSharedMaterials = null;
            Debug.Log("[MaterialEffectController] Preview OFF: Baseline restored.");
        }

        /// <summary>
        /// Editor-only: Save current renderer's sharedMaterials to target asset.
        /// Only works in Edit Mode. Validates that all materials are persistent assets.
        /// </summary>
        public void EditorSaveSnapshotTo(MaterialSetMaterialEffectAsset targetAsset)
        {
            if (Application.isPlaying)
            {
                Debug.LogError("[MaterialEffectController] _EditorSaveSnapshotTo: Cannot save during Play Mode.");
                return;
            }

            if (targetAsset == null)
            {
                Debug.LogError("[MaterialEffectController] _EditorSaveSnapshotTo: targetAsset is null.");
                return;
            }

            var renderer = _EditorGetRenderer();
            if (renderer == null)
            {
                Debug.LogError("[MaterialEffectController] _EditorSaveSnapshotTo: No Renderer found on this GameObject.");
                return;
            }

            var materials = renderer.sharedMaterials;
            if (materials == null || materials.Length == 0)
            {
                Debug.LogError("[MaterialEffectController] _EditorSaveSnapshotTo: Renderer has no materials.");
                return;
            }

            // Validate all materials
            const string DevianClonePrefix = "__DevianClone__";
            for (int i = 0; i < materials.Length; i++)
            {
                var mat = materials[i];

                // Check null
                if (mat == null)
                {
                    Debug.LogError($"[MaterialEffectController] _EditorSaveSnapshotTo: Slot [{i}] is null. Cannot save.");
                    return;
                }

                // Check if it's a persistent asset
                if (!UnityEditor.AssetDatabase.Contains(mat))
                {
                    Debug.LogError($"[MaterialEffectController] _EditorSaveSnapshotTo: Slot [{i}] '{mat.name}' is not a persistent asset (runtime instance). Cannot save.");
                    return;
                }

                // Check hideFlags
                if ((mat.hideFlags & HideFlags.DontSave) != 0 ||
                    (mat.hideFlags & HideFlags.DontSaveInEditor) != 0 ||
                    (mat.hideFlags & HideFlags.DontSaveInBuild) != 0)
                {
                    Debug.LogError($"[MaterialEffectController] _EditorSaveSnapshotTo: Slot [{i}] '{mat.name}' has DontSave hideFlags. Cannot save.");
                    return;
                }

                // Check Devian clone prefix
                if (mat.name != null && mat.name.StartsWith(DevianClonePrefix))
                {
                    Debug.LogError($"[MaterialEffectController] _EditorSaveSnapshotTo: Slot [{i}] '{mat.name}' is a Devian clone. Cannot save clones as effect source.");
                    return;
                }
            }

            // All validations passed - save
            var materialsCopy = new Material[materials.Length];
            System.Array.Copy(materials, materialsCopy, materials.Length);

            targetAsset.SetMaterialsForEditor(materialsCopy);
            Debug.Log($"[MaterialEffectController] Snapshot saved to '{targetAsset.name}' ({materialsCopy.Length} materials).");
        }
#endif
    }
}

