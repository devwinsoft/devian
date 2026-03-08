using System;
using System.Threading;
using System.Threading.Tasks;
using Devian.Domain.Common;

namespace Devian
{
    /// <summary>
    /// Legacy wrapper.
    /// Season reward logic moved to LeaderboardManager.
    /// </summary>
    [Obsolete("Integrated into LeaderboardManager. Use LeaderboardManager only.")]
    internal sealed class LeaderboardSeasonRewardManager : CompoSingleton<LeaderboardSeasonRewardManager>
    {
        public LeaderboardSeasonRewardStorage Storage
        {
            get
            {
                if (LeaderboardManager.TryGet(out var manager) && manager != null)
                    return manager.Storage;

                return null;
            }
        }

        public bool IsInitialized
        {
            get
            {
                if (LeaderboardManager.TryGet(out var manager) && manager != null)
                    return manager.IsInitialized;

                return false;
            }
        }

        public Task<CommonResult> InitializeAsync(CancellationToken ct = default)
        {
            if (LeaderboardManager.TryGet(out var manager) && manager != null)
                return manager.InitializeAsync(ct);

            return Task.FromResult(
                CommonResult.Failure(CommonErrorType.COMMON_INVALID_ARGUMENT, "LeaderboardManager is not initialized."));
        }

        public void ClearStorage()
        {
            if (LeaderboardManager.TryGet(out var manager) && manager != null)
                manager.ClearStorage();
        }

        public Task<CommonResult> SyncSeasonTransitionRewardsAsync(CancellationToken ct = default)
        {
            if (LeaderboardManager.TryGet(out var manager) && manager != null)
                return manager.SyncSeasonTransitionRewardsAsync(ct);

            return Task.FromResult(
                CommonResult.Failure(CommonErrorType.COMMON_INVALID_ARGUMENT, "LeaderboardManager is not initialized."));
        }

        protected override void onInitAwake()
        {
        }
    }
}
