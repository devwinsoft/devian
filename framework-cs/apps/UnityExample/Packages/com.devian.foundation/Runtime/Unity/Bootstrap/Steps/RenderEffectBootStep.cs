using System.Collections;
using UnityEngine;

namespace Devian
{
    /// <summary>
    /// RenderEffectAsset을 AssetManager 캐시에 적재하는 부트스텝.
    /// Addressables key를 인스펙터에서 입력받아 로드한다.
    /// </summary>
    public sealed class RenderEffectBootStep : MonoBehaviour, IDevianBootStep
    {
        [Tooltip("Boot order. Lower values execute first.")]
        [SerializeField] private int _order = 100;

        [Tooltip("Addressables key/label for RenderEffectAsset bundle.")]
        [SerializeField] private string _addressablesKey;

        public int Order => _order;

        public IEnumerator Boot()
        {
            if (string.IsNullOrWhiteSpace(_addressablesKey))
            {
                Debug.LogWarning("[RenderEffectBootStep] addressablesKey is empty. Skipping RenderEffect loading.");
                yield break;
            }

            Debug.Log($"[RenderEffectBootStep] Loading RenderEffectAssets with key: {_addressablesKey}");

            // AssetManager.LoadBundleAssets는 IEnumerator를 반환하므로 코루틴으로 실행
            yield return AssetManager.LoadBundleAssets<RenderEffectAsset>(_addressablesKey);

            Debug.Log("[RenderEffectBootStep] RenderEffectAssets loading completed.");
        }
    }
}
