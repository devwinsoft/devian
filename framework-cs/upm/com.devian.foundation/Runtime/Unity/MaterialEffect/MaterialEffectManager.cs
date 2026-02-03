// SSOT: skills/devian-unity/30-unity-components/28-render-controller/SKILL.md

using System.Collections;
using UnityEngine;

namespace Devian
{
    /// <summary>
    /// MaterialEffectAsset을 AssetManager 캐시에 적재하는 CompoSingleton 매니저.
    /// Addressables key를 인스펙터에서 입력받아 로드한다.
    /// Bootstrap.prefab에 붙여서 사용하면 Start()에서 자동 실행된다.
    ///
    /// CompoSingleton-based: Bootstrap prefab에 포함되어 부팅 시 자동 등록.
    /// </summary>
    public sealed class MaterialEffectManager : CompoSingleton<MaterialEffectManager>
    {
        [Tooltip("Addressables key/label for MaterialEffectAsset bundle.")]
        [SerializeField] private string _addressablesKey;

        private IEnumerator Start()
        {
            if (string.IsNullOrWhiteSpace(_addressablesKey))
            {
                Debug.LogWarning("[MaterialEffectManager] addressablesKey is empty. Skipping MaterialEffect loading.");
                yield break;
            }

            Debug.Log($"[MaterialEffectManager] Loading MaterialEffectAssets with key: {_addressablesKey}");

            // AssetManager.LoadBundleAssets는 IEnumerator를 반환하므로 코루틴으로 실행
            yield return AssetManager.LoadBundleAssets<MaterialEffectAsset>(_addressablesKey);

            Debug.Log("[MaterialEffectManager] MaterialEffectAssets loading completed.");
        }
    }
}
