// SSOT: skills/devian-unity/30-unity-components/28-render-controller/SKILL.md

using System.Collections;
using UnityEngine;

namespace Devian
{
    /// <summary>
    /// RenderEffectAsset을 AssetManager 캐시에 적재하는 CompoSingleton 매니저.
    /// Addressables key를 인스펙터에서 입력받아 로드한다.
    /// BootstrapRoot.prefab에 붙여서 사용하면 Start()에서 자동 실행된다.
    ///
    /// CompoSingleton-based: BootstrapRoot prefab에 포함되어 부팅 시 자동 등록.
    /// </summary>
    public sealed class RenderEffectManager : CompoSingleton<RenderEffectManager>
    {
        [Tooltip("Addressables key/label for RenderEffectAsset bundle.")]
        [SerializeField] private string _addressablesKey;

        private IEnumerator Start()
        {
            if (string.IsNullOrWhiteSpace(_addressablesKey))
            {
                Debug.LogWarning("[RenderEffectManager] addressablesKey is empty. Skipping RenderEffect loading.");
                yield break;
            }

            Debug.Log($"[RenderEffectManager] Loading RenderEffectAssets with key: {_addressablesKey}");

            // AssetManager.LoadBundleAssets는 IEnumerator를 반환하므로 코루틴으로 실행
            yield return AssetManager.LoadBundleAssets<RenderEffectAsset>(_addressablesKey);

            Debug.Log("[RenderEffectManager] RenderEffectAssets loading completed.");
        }
    }
}
