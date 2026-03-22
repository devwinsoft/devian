// SSOT: skills/devian-unity/50-mobile-package/12-mobile-bundle-manager/SKILL.md

#nullable enable

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using Devian.Domain.Common;

namespace Devian
{
    /// <summary>
    /// MobilePackage 레이어의 BundleManager 중간 추상 클래스.
    /// PatchLabels를 abstract property로 정의하고,
    /// 파라미터 없는 InitializeAsync/DownloadAsync를 제공한다.
    /// onLoadBundlesAsync에서 UIManager.LoadBundlesAsync()를 호출한다.
    /// </summary>
    public abstract class MobileBundleManager<T> : BundleManager<T> where T : MobileBundleManager<T>
    {
        /// <summary>
        /// 패치/다운로드 대상 라벨 목록. concrete 서브클래스가 정의한다.
        /// </summary>
        protected abstract IReadOnlyList<string> PatchLabels { get; }

        /// <summary>
        /// PatchLabels 기준으로 다운로드 필요 용량을 계산한다.
        /// </summary>
        public Task<CommonResult<PatchInfo>> InitializeAsync()
        {
            return base.InitializeAsync(PatchLabels);
        }

        /// <summary>
        /// PatchLabels 기준으로 의존 번들을 다운로드한다.
        /// </summary>
        public Task<CommonResult> DownloadAsync(Action<float>? onProgress = null)
        {
            return base.DownloadAsync(PatchLabels, onProgress);
        }

        /// <summary>
        /// UIManager.LoadBundlesAsync()를 호출하여 UI 번들 에셋을 로드한다.
        /// concrete 서브클래스는 base.onLoadBundlesAsync()를 호출한 후 추가 로드를 수행한다.
        /// </summary>
        protected override async Task onLoadBundlesAsync(SystemLanguage language, Action<float>? onProgress = null)
        {
            await UIManager.Instance.LoadBundlesAsync();
        }
    }
}
