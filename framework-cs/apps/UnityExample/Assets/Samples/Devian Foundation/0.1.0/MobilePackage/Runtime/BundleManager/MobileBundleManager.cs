// SSOT: skills/devian-unity/50-mobile-package/12-mobile-bundle-manager/SKILL.md

#nullable enable

using System;
using System.Threading.Tasks;
using UnityEngine;

namespace Devian
{
    /// <summary>
    /// MobilePackage 레이어의 BundleManager 중간 추상 클래스.
    /// LoadBundlesAsync에서 UIManager.LoadBundlesAsync()를 호출한다.
    /// </summary>
    public abstract class MobileBundleManager<T> : BundleManager<T> where T : MobileBundleManager<T>
    {
        /// <summary>
        /// UIManager.LoadBundlesAsync()를 호출하여 UI 번들 에셋을 로드한다.
        /// concrete 서브클래스는 base.LoadBundlesAsync()를 호출한 후 추가 로드를 수행한다.
        /// </summary>
        public override async Task LoadBundlesAsync(SystemLanguage language, float startProgress, Action<float>? onProgress = null)
        {
            await UIManager.Instance.LoadBundlesAsync();
        }
    }
}
