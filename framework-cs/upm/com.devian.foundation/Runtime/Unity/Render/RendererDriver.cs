using System.Collections.Generic;
using UnityEngine;

namespace Devian
{
    /// <summary>
    /// IRenderDriver 구현체.
    /// Renderer[]를 관리하며 Material/PropertyBlock 조작을 제공한다.
    /// </summary>
    public sealed class RendererDriver : MonoBehaviour, IRenderDriver
    {
        [Tooltip("Renderers to control. If empty, will auto-collect from children.")]
        [SerializeField] private Renderer[] _renderers;

        [Tooltip("Include inactive renderers when auto-collecting.")]
        [SerializeField] private bool _includeInactive = true;

        private Material[][] _baselineMaterials;
        private bool _baselineCaptured;

        public bool IsValid => _renderers != null && _renderers.Length > 0;
        public int RendererCount => _renderers != null ? _renderers.Length : 0;

        private void Awake()
        {
            // 자동 수집
            if (_renderers == null || _renderers.Length == 0)
            {
                _renderers = GetComponentsInChildren<Renderer>(_includeInactive);
            }
        }

        public void CaptureBaseline()
        {
            if (_renderers == null || _renderers.Length == 0)
                return;

            _baselineMaterials = new Material[_renderers.Length][];

            for (int i = 0; i < _renderers.Length; i++)
            {
                var renderer = _renderers[i];
                if (renderer == null)
                {
                    _baselineMaterials[i] = null;
                    continue;
                }

                var mats = renderer.sharedMaterials;
                if (mats != null && mats.Length > 0)
                {
                    _baselineMaterials[i] = new Material[mats.Length];
                    for (int j = 0; j < mats.Length; j++)
                    {
                        _baselineMaterials[i][j] = mats[j];
                    }
                }
                else
                {
                    _baselineMaterials[i] = null;
                }
            }

            _baselineCaptured = true;
        }

        public void RestoreBaseline()
        {
            if (!_baselineCaptured || _baselineMaterials == null)
                return;

            for (int i = 0; i < _renderers.Length; i++)
            {
                var renderer = _renderers[i];
                if (renderer == null)
                    continue;

                // Material 복구
                if (_baselineMaterials[i] != null)
                {
                    renderer.sharedMaterials = _baselineMaterials[i];
                }

                // PropertyBlock 클리어
                renderer.SetPropertyBlock(null);
            }
        }

        public void SetSharedMaterial(int rendererIndex, Material material)
        {
            if (!IsValidIndex(rendererIndex))
                return;

            _renderers[rendererIndex].sharedMaterial = material;
        }

        public void SetSharedMaterials(int rendererIndex, Material[] materials)
        {
            if (!IsValidIndex(rendererIndex))
                return;

            _renderers[rendererIndex].sharedMaterials = materials;
        }

        public void ClearPropertyBlock(int rendererIndex)
        {
            if (!IsValidIndex(rendererIndex))
                return;

            _renderers[rendererIndex].SetPropertyBlock(null);
        }

        public void SetPropertyBlock(int rendererIndex, MaterialPropertyBlock block)
        {
            if (!IsValidIndex(rendererIndex))
                return;

            _renderers[rendererIndex].SetPropertyBlock(block);
        }

        public void SetVisible(bool visible)
        {
            if (_renderers == null)
                return;

            for (int i = 0; i < _renderers.Length; i++)
            {
                if (_renderers[i] != null)
                {
                    _renderers[i].enabled = visible;
                }
            }
        }

        private bool IsValidIndex(int index)
        {
            return _renderers != null && index >= 0 && index < _renderers.Length && _renderers[index] != null;
        }
    }
}
