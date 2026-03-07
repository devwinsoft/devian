#if UNITY_INCLUDE_TESTS
using System;
using NUnit.Framework;

namespace Devian
{
    public sealed class LeaderboardSeasonRewardManagerTests
    {
        [Test]
        public void IsSeasonRewardEvaluationReady_ShouldReturnFalse_AtNineMinutesAfterSeasonEnd()
        {
            const long seasonEndUtcMs = 1_000_000L;
            var serverNowUtcMs = seasonEndUtcMs + (long)TimeSpan.FromMinutes(9).TotalMilliseconds;

            var ready = LeaderboardSeasonRewardManager.IsSeasonRewardEvaluationReady(seasonEndUtcMs, serverNowUtcMs);

            Assert.That(ready, Is.False);
        }

        [Test]
        public void IsSeasonRewardEvaluationReady_ShouldReturnTrue_AtTenMinutesAfterSeasonEnd()
        {
            const long seasonEndUtcMs = 1_000_000L;
            var serverNowUtcMs = seasonEndUtcMs + LeaderboardSeasonRewardManager.SeasonRewardGracePeriodMs;

            var ready = LeaderboardSeasonRewardManager.IsSeasonRewardEvaluationReady(seasonEndUtcMs, serverNowUtcMs);

            Assert.That(ready, Is.True);
        }
    }
}
#endif
