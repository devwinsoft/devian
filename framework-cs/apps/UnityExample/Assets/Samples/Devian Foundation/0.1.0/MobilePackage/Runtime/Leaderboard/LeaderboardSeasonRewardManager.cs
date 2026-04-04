using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Devian.Domain.Common;
using Devian.Domain.Game;
using UnityEngine;

namespace Devian
{
    public sealed class LeaderboardSeasonRewardManager : CompoSingleton<LeaderboardSeasonRewardManager>
    {
        private const string Tag = nameof(LeaderboardSeasonRewardManager);
        private static readonly TimeSpan SeasonRewardGracePeriod = TimeSpan.FromMinutes(10);
        internal static long SeasonRewardGracePeriodMs => (long)SeasonRewardGracePeriod.TotalMilliseconds;

        private readonly LeaderboardSeasonRewardStorage _storage = new();
        private readonly SemaphoreSlim _initializeGate = new(1, 1);
        private bool _initialized;

        public LeaderboardSeasonRewardStorage Storage => _storage;
        public bool IsInitialized => _initialized;

        public async Task<GameResult> InitializeAsync(CancellationToken ct = default)
        {
            await _initializeGate.WaitAsync(ct);
            try
            {
                if (_initialized)
                    return GameResult.Ok();

                _initialized = true;
                return GameResult.Ok();
            }
            finally
            {
                _initializeGate.Release();
            }
        }

        public void ClearStorage()
        {
            _storage.Clear();
        }

        public async Task<GameResult> SyncSeasonTransitionRewardsAsync(CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();

            if (!_initialized)
                return GameResult.Failure(GAME_ERROR_TYPE.GAME_INVALID_ARGUMENT, "LeaderboardSeasonRewardManager is not initialized.");

            if (!RemoteDataManager.TryGetServerNowUtcMs(out var serverNowUtcMs)
                || serverNowUtcMs <= 0L)
                return GameResult.Ok();

            if (!LeaderboardManager.TryGet(out var leaderboardManager) || leaderboardManager == null)
                return GameResult.Failure(GAME_ERROR_TYPE.GAME_INVALID_ARGUMENT, "LeaderboardManager is not initialized.");

            var activeRows = collectActiveSeasonRows();
            if (activeRows.Count <= 0)
                return GameResult.Ok();

            var hasAnyClaimWrite = false;
            foreach (var mode in GetAllModes())
            {
                if (mode == LEADERBOARD_MODE.NONE)
                    continue;

                if (!tryFindCurrentSeasonStart(activeRows, mode, serverNowUtcMs, out var currentSeasonStartUtcMs))
                    continue;

                if (!tryFindPreviousSeasonRow(activeRows, mode, currentSeasonStartUtcMs, out var prevRow) || prevRow == null)
                    continue;

                if (!IsSeasonRewardEvaluationReady(getSeasonEndUtcMs(prevRow), serverNowUtcMs))
                    continue;

                var claimKey = buildClaimKey(prevRow.leaderboard_id);
                if (_storage.TryGetClaim(claimKey, out _))
                    continue;

                var snapshotResult = await leaderboardManager.GetPlayerSnapshotAsync(prevRow.leaderboard_id, ct);
                if (snapshotResult.IsFailure)
                {
                    Debug.LogWarning($"[{Tag}] Snapshot load failed: leaderboard_id={prevRow.leaderboard_id}, error={snapshotResult.Error}");
                    continue;
                }

                var snapshot = snapshotResult.Value;
                if (snapshot.Status == LeaderboardManager.LeaderboardPlayerSnapshotStatus.PlatformUnavailable
                    || snapshot.Status == LeaderboardManager.LeaderboardPlayerSnapshotStatus.NotLoggedIn
                    || snapshot.Status == LeaderboardManager.LeaderboardPlayerSnapshotStatus.Failed)
                {
                    continue;
                }

                if (snapshot.Status == LeaderboardManager.LeaderboardPlayerSnapshotStatus.NoScore || !snapshot.HasScore)
                {
                    _storage.SetClaim(claimKey, new LeaderboardSeasonRewardClaimRecord
                    {
                        resultType = LeaderboardSeasonRewardResultType.NO_PARTICIPATION,
                        rank = 0L,
                        score = 0L,
                        rewardGroupId = string.Empty,
                        evaluatedAtServerUtcMs = serverNowUtcMs,
                    });
                    hasAnyClaimWrite = true;
                    continue;
                }

                if (!tryResolveRewardGroupId(prevRow.leaderboard_id, snapshot.Rank, out var rewardGroupId))
                {
                    _storage.SetClaim(claimKey, new LeaderboardSeasonRewardClaimRecord
                    {
                        resultType = LeaderboardSeasonRewardResultType.RANK_OUT_OF_REWARD,
                        rank = snapshot.Rank,
                        score = snapshot.Score,
                        rewardGroupId = string.Empty,
                        evaluatedAtServerUtcMs = serverNowUtcMs,
                    });
                    hasAnyClaimWrite = true;
                    continue;
                }

                var apply = RewardManager.Instance.ApplyRewardGroup(rewardGroupId);
                if (apply.IsFailure)
                {
                    Debug.LogWarning($"[{Tag}] Reward apply failed: leaderboard_id={prevRow.leaderboard_id}, reward_group_id={rewardGroupId}, error={apply.Error}");
                    continue;
                }

                _storage.SetClaim(claimKey, new LeaderboardSeasonRewardClaimRecord
                {
                    resultType = LeaderboardSeasonRewardResultType.CLAIMED,
                    rank = snapshot.Rank,
                    score = snapshot.Score,
                    rewardGroupId = rewardGroupId,
                    evaluatedAtServerUtcMs = serverNowUtcMs,
                });
                hasAnyClaimWrite = true;
            }

            if (!hasAnyClaimWrite)
                return GameResult.Ok();

            var save = await SaveDataManager.Instance.SaveGameStorageAsync(true, ct);
            if (save.IsFailure)
                return GameResult.Failure(GAME_ERROR_TYPE.GAME_INVALID_ARGUMENT, save.Error!.Message, $"commonCode={save.Error.Code}");

            return GameResult.Ok();
        }

        private static LEADERBOARD_MODE[] GetAllModes()
        {
            return (LEADERBOARD_MODE[])Enum.GetValues(typeof(LEADERBOARD_MODE));
        }

        private static List<LEADERBOARD> collectActiveSeasonRows()
        {
            var rows = new List<LEADERBOARD>();
            foreach (var row in TB_LEADERBOARD.GetAll())
            {
                if (isActiveSeasonRow(row))
                    rows.Add(row);
            }

            return rows;
        }

        private static bool isActiveSeasonRow(LEADERBOARD row)
        {
            if (row == null || !row.is_active)
                return false;

            if (string.IsNullOrWhiteSpace(row.leaderboard_id))
            {
                return false;
            }

            var seasonStartUtcMs = getSeasonStartUtcMs(row);
            var seasonEndUtcMs = getSeasonEndUtcMs(row);
            if (seasonStartUtcMs <= 0L || seasonEndUtcMs <= seasonStartUtcMs)
                return false;

            return row.mode != LEADERBOARD_MODE.NONE;
        }

        private static bool tryFindCurrentSeasonStart(
            List<LEADERBOARD> rows,
            LEADERBOARD_MODE mode,
            long serverNowUtcMs,
            out long seasonStartUtcMs)
        {
            seasonStartUtcMs = 0L;
            var found = false;
            foreach (var row in rows)
            {
                if (row == null || row.mode != mode)
                    continue;

                var seasonStartUtcMsValue = getSeasonStartUtcMs(row);
                var seasonEndUtcMsValue = getSeasonEndUtcMs(row);
                if (serverNowUtcMs < seasonStartUtcMsValue || serverNowUtcMs >= seasonEndUtcMsValue)
                    continue;

                if (!found || seasonStartUtcMsValue < seasonStartUtcMs)
                {
                    seasonStartUtcMs = seasonStartUtcMsValue;
                    found = true;
                }
            }

            return found;
        }

        private static bool tryFindPreviousSeasonRow(
            List<LEADERBOARD> rows,
            LEADERBOARD_MODE mode,
            long currentSeasonStartUtcMs,
            out LEADERBOARD seasonRow)
        {
            seasonRow = null;
            long latestEndUtcMs = long.MinValue;

            foreach (var row in rows)
            {
                if (row == null || row.mode != mode)
                    continue;

                var seasonEndUtcMs = getSeasonEndUtcMs(row);
                if (seasonEndUtcMs > currentSeasonStartUtcMs)
                    continue;

                if (seasonEndUtcMs > latestEndUtcMs)
                {
                    latestEndUtcMs = seasonEndUtcMs;
                    seasonRow = row;
                    continue;
                }

                if (seasonEndUtcMs == latestEndUtcMs
                    && seasonRow != null
                    && string.CompareOrdinal(row.leaderboard_id, seasonRow.leaderboard_id) < 0)
                {
                    seasonRow = row;
                }
            }

            return seasonRow != null;
        }

        private static string buildClaimKey(string leaderboardId)
        {
            return (leaderboardId ?? string.Empty).Trim();
        }

        private static bool tryResolveRewardGroupId(string leaderboardId, long rank, out string rewardGroupId)
        {
            rewardGroupId = string.Empty;
            if (string.IsNullOrWhiteSpace(leaderboardId) || rank <= 0L)
                return false;

            var rewardRows = TB_LEADERBOARD_REWARD.GetByGroup(leaderboardId.Trim());
            if (rewardRows == null || rewardRows.Count <= 0)
                return false;

            for (var i = 0; i < rewardRows.Count; i++)
            {
                var row = rewardRows[i];
                if (row == null)
                    continue;

                if (row.rank_from <= 0L || row.rank_to < row.rank_from)
                    continue;

                if (rank < row.rank_from || rank > row.rank_to)
                    continue;

                if (string.IsNullOrWhiteSpace(row.reward_group_id))
                    return false;

                rewardGroupId = row.reward_group_id.Trim();
                return rewardGroupId.Length > 0;
            }

            return false;
        }

        private static long getSeasonStartUtcMs(LEADERBOARD row)
        {
            if (row == null || string.IsNullOrEmpty(row.season_id))
                return 0L;
            var season = TB_SEASON.Get(row.season_id);
            return season?.start_utc_time?.utcTimeMs ?? 0L;
        }

        private static long getSeasonEndUtcMs(LEADERBOARD row)
        {
            if (row == null || string.IsNullOrEmpty(row.season_id))
                return 0L;
            var season = TB_SEASON.Get(row.season_id);
            return season?.end_utc_time?.utcTimeMs ?? 0L;
        }

        internal static bool IsSeasonRewardEvaluationReady(long prevSeasonEndUtcMs, long serverNowUtcMs)
        {
            return serverNowUtcMs >= prevSeasonEndUtcMs + SeasonRewardGracePeriodMs;
        }
    }
}
