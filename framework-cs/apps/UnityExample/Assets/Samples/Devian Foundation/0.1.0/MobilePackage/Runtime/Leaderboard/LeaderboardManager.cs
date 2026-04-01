using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Devian.Domain.Common;
using Devian.Domain.Game;
using UnityEngine;
using UnityEngine.SocialPlatforms;

namespace Devian
{
    /// <summary>
    /// Leaderboard facade.
    /// Public API only accepts internal leaderboard IDs.
    /// </summary>
    public sealed class LeaderboardManager : CompoSingleton<LeaderboardManager>
    {
        private const string Tag = nameof(LeaderboardManager);
        private static readonly TimeSpan SeasonRewardGracePeriod = TimeSpan.FromMinutes(10);
        internal static long SeasonRewardGracePeriodMs => (long)SeasonRewardGracePeriod.TotalMilliseconds;

        private enum RuntimePlatformKind
        {
            Unsupported = 0,
            Apple = 1,
            Google = 2,
        }

        public enum LeaderboardPlayerSnapshotStatus
        {
            Success = 0,
            NoScore = 1,
            PlatformUnavailable = 2,
            NotLoggedIn = 3,
            Failed = 4,
        }

        public readonly struct LeaderboardPlayerSnapshot
        {
            public LeaderboardPlayerSnapshot(
                string leaderboardId,
                long score,
                long rank,
                bool hasScore,
                LeaderboardPlayerSnapshotStatus status)
            {
                LeaderboardId = leaderboardId ?? string.Empty;
                Score = score;
                Rank = rank;
                HasScore = hasScore;
                Status = status;
            }

            public string LeaderboardId { get; }
            public long Score { get; }
            public long Rank { get; }
            public bool HasScore { get; }
            public LeaderboardPlayerSnapshotStatus Status { get; }
        }

        private sealed class LeaderboardMapEntry
        {
            public string leaderboardId = string.Empty;
            public bool isActive = true;
            public string appleLeaderboardId = string.Empty;
            public string googleLeaderboardId = string.Empty;
            public string messageId = string.Empty;

            public string InternalId => (leaderboardId ?? string.Empty).Trim();

            public string ResolvePlatformId(RuntimePlatformKind platform)
            {
                switch (platform)
                {
                    case RuntimePlatformKind.Apple:
                        return (appleLeaderboardId ?? string.Empty).Trim();
                    case RuntimePlatformKind.Google:
                        return (googleLeaderboardId ?? string.Empty).Trim();
                    default:
                        return string.Empty;
                }
            }
        }

        private interface ILeaderboardPlatformAdapter
        {
            Task<CommonResult> InitializeAsync(CancellationToken ct);
            Task<CommonResult> ReportScoreAsync(string platformLeaderboardId, long score, CancellationToken ct);
            Task<CommonResult<LeaderboardPlayerSnapshot>> LoadPlayerSnapshotAsync(
                string platformLeaderboardId,
                string internalLeaderboardId,
                CancellationToken ct);
        }

        private readonly Dictionary<string, LeaderboardMapEntry> _leaderboardById
            = new Dictionary<string, LeaderboardMapEntry>(StringComparer.Ordinal);
        private readonly LeaderboardSeasonRewardStorage _storage = new();

        private readonly SemaphoreSlim _initializeGate = new SemaphoreSlim(1, 1);
        private static readonly CBigInt LongMaxScore = CBigInt.FromLong(long.MaxValue);
        private bool _initializeFromApplicationRequested;

        private ILeaderboardPlatformAdapter _adapter;
        private bool _initialized;
        public bool IsInitialized => _initialized;
        public LeaderboardSeasonRewardStorage Storage => _storage;

        protected override void onInitAwake()
        {
            rebuildMappingCaches();
            tryInitializeFromApplication();
        }

        private void Start()
        {
            tryInitializeFromApplication();
        }

        public async Task<CommonResult> InitializeAsync(CancellationToken ct = default)
        {
            await _initializeGate.WaitAsync(ct);
            try
            {
                if (_initialized)
                    return CommonResult.Ok();

                rebuildMappingCaches();
                _adapter = _adapter ?? createAdapter(getRuntimePlatform());

                var init = await _adapter.InitializeAsync(ct);
                if (init.IsFailure)
                    return CommonResult.Failure(init.Error!);

                _initialized = true;
                return CommonResult.Ok();
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

        public async Task<CommonResult> ReportScoreAsync(string leaderboardId, CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();

            var init = await InitializeAsync(ct);
            if (init.IsFailure)
                return init;

            var resolve = tryResolveLeaderboardPlatformId(leaderboardId, out var entry, out var platformLeaderboardId);
            if (resolve.IsFailure)
                return resolve;

            var seasonCheck = ensureWithinSeasonWindow(leaderboardId);
            if (seasonCheck.IsFailure)
                return seasonCheck;

            var scoreResolve = tryResolveLeaderboardScore(entry!, out var score);
            if (scoreResolve.IsFailure)
                return scoreResolve;

            return await _adapter.ReportScoreAsync(platformLeaderboardId, score, ct);
        }

        public async Task<CommonResult<LeaderboardPlayerSnapshot>> GetPlayerSnapshotAsync(
            string leaderboardId,
            CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();

            var init = await InitializeAsync(ct);
            if (init.IsFailure)
                return CommonResult<LeaderboardPlayerSnapshot>.Failure(init.Error!);

            var resolve = tryResolveLeaderboardPlatformId(leaderboardId, out var entry, out var platformLeaderboardId);
            if (resolve.IsFailure)
                return CommonResult<LeaderboardPlayerSnapshot>.Failure(resolve.Error!);

            return await _adapter.LoadPlayerSnapshotAsync(platformLeaderboardId, entry!.InternalId, ct);
        }

        public async Task<CommonResult> SyncSeasonTransitionRewardsAsync(CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();

            var init = await InitializeAsync(ct);
            if (init.IsFailure)
                return init;

            if (!RemoteDataManager.TryGetServerNowUtcMs(out var serverNowUtcMs)
                || serverNowUtcMs <= 0L)
                return CommonResult.Ok();

            var activeRows = collectActiveSeasonRows();
            if (activeRows.Count <= 0)
                return CommonResult.Ok();

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

                var snapshotResult = await GetPlayerSnapshotAsync(prevRow.leaderboard_id, ct);
                if (snapshotResult.IsFailure)
                {
                    Debug.LogWarning($"[{Tag}] Snapshot load failed: leaderboard_id={prevRow.leaderboard_id}, error={snapshotResult.Error}");
                    continue;
                }

                var snapshot = snapshotResult.Value;
                if (snapshot.Status == LeaderboardPlayerSnapshotStatus.PlatformUnavailable
                    || snapshot.Status == LeaderboardPlayerSnapshotStatus.NotLoggedIn
                    || snapshot.Status == LeaderboardPlayerSnapshotStatus.Failed)
                {
                    continue;
                }

                if (snapshot.Status == LeaderboardPlayerSnapshotStatus.NoScore || !snapshot.HasScore)
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
                return CommonResult.Ok();

            var save = await SaveDataManager.Instance.SaveGameStorageAsync(true, ct);
            if (save.IsFailure)
                return CommonResult.Failure(save.Error!);

            return CommonResult.Ok();
        }

        [Obsolete("Use ReportScoreAsync(string leaderboard_id, CancellationToken ct = default). Score is resolved from LEADERBOARD.message_id.")]
        public Task<CommonResult> ReportScoreAsync(string leaderboardId, long score, CancellationToken ct = default)
        {
            return ReportScoreAsync(leaderboardId, ct);
        }

        private CommonResult tryResolveLeaderboardPlatformId(string leaderboardId, out LeaderboardMapEntry entry, out string platformLeaderboardId)
        {
            entry = null;
            platformLeaderboardId = string.Empty;

            if (string.IsNullOrWhiteSpace(leaderboardId))
            {
                return CommonResult.Failure(
                    COMMON_ERROR_TYPE.COMMON_INVALID_ARGUMENT,
                    "leaderboard_id is empty.");
            }

            if (!_leaderboardById.TryGetValue(leaderboardId.Trim(), out entry) || entry == null || !entry.isActive)
            {
                return CommonResult.Failure(
                    COMMON_ERROR_TYPE.LEADERBOARD_NOT_FOUND,
                    $"Active leaderboard mapping not found: {leaderboardId}");
            }

            platformLeaderboardId = entry.ResolvePlatformId(getRuntimePlatform());
            if (string.IsNullOrEmpty(platformLeaderboardId))
            {
                return CommonResult.Failure(
                    COMMON_ERROR_TYPE.LEADERBOARD_PLATFORM_ID_MISSING,
                    $"Platform leaderboard ID mapping missing: {leaderboardId}");
            }

            return CommonResult.Ok();
        }

        private CommonResult tryResolveLeaderboardScore(LeaderboardMapEntry entry, out long score)
        {
            score = 0L;
            if (entry == null)
            {
                return CommonResult.Failure(
                    COMMON_ERROR_TYPE.COMMON_INVALID_ARGUMENT,
                    "Leaderboard entry is null.");
            }

            var messageId = (entry.messageId ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(messageId))
            {
                return CommonResult.Failure(
                    COMMON_ERROR_TYPE.COMMON_INVALID_ARGUMENT,
                    $"LEADERBOARD.message_id is empty: {entry.InternalId}");
            }

            var message = TB_GAME_MESSAGE.Get(messageId);
            if (message == null)
            {
                return CommonResult.Failure(
                    COMMON_ERROR_TYPE.LEADERBOARD_MESSAGE_NOT_FOUND,
                    $"GAME_MESSAGE not found for leaderboard score: leaderboard_id={entry.InternalId}, message_id={messageId}");
            }

            if (!isLeaderboardSupportedSaveType(message.save_type))
            {
                return CommonResult.Failure(
                    COMMON_ERROR_TYPE.LEADERBOARD_MESSAGE_SAVE_TYPE_INVALID,
                    $"Invalid GAME_MESSAGE.saveType for leaderboard score: " +
                    $"leaderboard_id={entry.InternalId}, message_id={messageId}, saveType={message.save_type}. " +
                    $"Expected TOTAL_SUM or TOTAL_MAX.");
            }

            if (!GameMessageManager.TryGet(out var messageManager) || messageManager == null)
            {
                return CommonResult.Failure(
                    COMMON_ERROR_TYPE.COMMON_INVALID_ARGUMENT,
                    "GameMessageManager is not initialized.");
            }

            var stat = messageManager.GetStat(messageId);
            if (stat < CBigInt.Zero)
            {
                return CommonResult.Failure(
                    COMMON_ERROR_TYPE.COMMON_INVALID_ARGUMENT,
                    $"Leaderboard score must be non-negative: leaderboard_id={entry.InternalId}, message_id={messageId}, value={stat}");
            }

            if (stat > LongMaxScore)
                Debug.LogWarning($"[{Tag}] Leaderboard score clamped to long.MaxValue. leaderboard_id={entry.InternalId}, message_id={messageId}, value={stat}");

            score = convertScoreToLong(stat);
            return CommonResult.Ok();
        }

        private static long convertScoreToLong(CBigInt value)
        {
            if (value <= CBigInt.Zero)
                return 0L;

            if (value >= LongMaxScore)
                return long.MaxValue;

            try
            {
                var numeric = (double)value;
                if (double.IsNaN(numeric) || numeric <= 0d)
                    return 0L;
                if (double.IsInfinity(numeric) || numeric >= long.MaxValue)
                    return long.MaxValue;

                return (long)Math.Floor(numeric);
            }
            catch (OverflowException)
            {
                return long.MaxValue;
            }
        }

        private static bool isLeaderboardSupportedSaveType(GAME_MESSAGE_SAVE_TYPE saveType)
        {
            return saveType == GAME_MESSAGE_SAVE_TYPE.TOTAL_SUM
                   || saveType == GAME_MESSAGE_SAVE_TYPE.TOTAL_MAX;
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
                return false;

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

        private static CommonResult ensureWithinSeasonWindow(string leaderboardId)
        {
            var row = TB_LEADERBOARD.Get((leaderboardId ?? string.Empty).Trim());
            if (row == null)
                return CommonResult.Ok();

            if (string.IsNullOrWhiteSpace(row.season_id))
                return CommonResult.Ok();

            var season = TB_SEASON.Get(row.season_id);
            if (season == null)
                return CommonResult.Failure(
                    COMMON_ERROR_TYPE.LEADERBOARD_SEASON_NOT_FOUND,
                    $"SEASON not found: leaderboard_id={leaderboardId}, season_id={row.season_id}");

            var startUtcMs = season.start_utc_time?.utcTimeMs ?? 0L;
            var endUtcMs = season.end_utc_time?.utcTimeMs ?? 0L;
            if (startUtcMs <= 0L || endUtcMs <= startUtcMs)
                return CommonResult.Failure(
                    COMMON_ERROR_TYPE.LEADERBOARD_SEASON_TIME_INVALID,
                    $"SEASON time range invalid: season_id={row.season_id}");

            if (!RemoteDataManager.TryGetServerNowUtcMs(out var serverNowUtcMs)
                || serverNowUtcMs <= 0L)
                return CommonResult.Failure(
                    COMMON_ERROR_TYPE.LEADERBOARD_SERVER_TIME_UNAVAILABLE,
                    "Server time unavailable for season check.");

            if (serverNowUtcMs < startUtcMs || serverNowUtcMs >= endUtcMs)
                return CommonResult.Failure(
                    COMMON_ERROR_TYPE.LEADERBOARD_SEASON_NOT_ACTIVE,
                    $"Score recording is only allowed during the active season: leaderboard_id={leaderboardId}, season_id={row.season_id}");

            return CommonResult.Ok();
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
            if (row == null || string.IsNullOrWhiteSpace(row.season_id))
                return 0L;
            var season = TB_SEASON.Get(row.season_id);
            return season?.start_utc_time?.utcTimeMs ?? 0L;
        }

        private static long getSeasonEndUtcMs(LEADERBOARD row)
        {
            if (row == null || string.IsNullOrWhiteSpace(row.season_id))
                return 0L;
            var season = TB_SEASON.Get(row.season_id);
            return season?.end_utc_time?.utcTimeMs ?? 0L;
        }

        internal static bool IsSeasonRewardEvaluationReady(long prevSeasonEndUtcMs, long serverNowUtcMs)
        {
            return serverNowUtcMs >= prevSeasonEndUtcMs + SeasonRewardGracePeriodMs;
        }

        private static LeaderboardPlayerSnapshot createSnapshot(
            string leaderboardId,
            long score,
            long rank,
            bool hasScore,
            LeaderboardPlayerSnapshotStatus status)
        {
            return new LeaderboardPlayerSnapshot(
                leaderboardId,
                Math.Max(0L, score),
                Math.Max(0L, rank),
                hasScore,
                status);
        }

        private void rebuildMappingCaches()
        {
            _leaderboardById.Clear();

            foreach (var row in TB_LEADERBOARD.GetAll())
            {
                if (row == null)
                    continue;

                var id = (row.leaderboard_id ?? string.Empty).Trim();
                if (string.IsNullOrEmpty(id))
                    continue;

                var entry = new LeaderboardMapEntry
                {
                    leaderboardId = id,
                    isActive = row.is_active,
                    appleLeaderboardId = row.apple_leaderboard_id ?? string.Empty,
                    googleLeaderboardId = row.google_leaderboard_id ?? string.Empty,
                    messageId = row.message_id ?? string.Empty,
                };

                if (_leaderboardById.ContainsKey(id))
                    Debug.LogWarning($"[{Tag}] Duplicate leaderboard_id mapping. override id={id}");

                _leaderboardById[id] = entry;
            }
        }

        private static RuntimePlatformKind getRuntimePlatform()
        {
#if UNITY_IOS && !UNITY_EDITOR
            return RuntimePlatformKind.Apple;
#elif UNITY_ANDROID && !UNITY_EDITOR
            return RuntimePlatformKind.Google;
#else
            return RuntimePlatformKind.Unsupported;
#endif
        }

        private static ILeaderboardPlatformAdapter createAdapter(RuntimePlatformKind platform)
        {
            switch (platform)
            {
                case RuntimePlatformKind.Apple:
                    return new AppleLeaderboardPlatformAdapter();
                case RuntimePlatformKind.Google:
                    return new GoogleLeaderboardPlatformAdapter();
                default:
                    return new UnsupportedLeaderboardPlatformAdapter();
            }
        }

        private void tryInitializeFromApplication()
        {
            if (_initializeFromApplicationRequested)
                return;

            if (MobileApplication.Instance == null)
                return;

            _initializeFromApplicationRequested = true;
            _ = initializeFromApplicationAsync();
        }

        private async Task initializeFromApplicationAsync()
        {
            var init = await InitializeAsync(CancellationToken.None);
            if (init.IsFailure)
            {
                Debug.LogWarning(
                    $"[{Tag}] InitializeAsync failed during application bootstrap (non-fatal): " +
                    $"{init.Error.Code}: {init.Error.Message}");
            }
        }

        private sealed class UnsupportedLeaderboardPlatformAdapter : ILeaderboardPlatformAdapter
        {
            public Task<CommonResult> InitializeAsync(CancellationToken ct)
                => Task.FromResult(CommonResult.Failure(
                    COMMON_ERROR_TYPE.LOGIN_UNSUPPORTED,
                    "Leaderboard is not supported on this platform."));

            public Task<CommonResult> ReportScoreAsync(string platformLeaderboardId, long score, CancellationToken ct)
                => Task.FromResult(CommonResult.Failure(
                    COMMON_ERROR_TYPE.LOGIN_UNSUPPORTED,
                    "Leaderboard is not supported on this platform."));

            public Task<CommonResult<LeaderboardPlayerSnapshot>> LoadPlayerSnapshotAsync(
                string platformLeaderboardId,
                string internalLeaderboardId,
                CancellationToken ct)
                => Task.FromResult(CommonResult<LeaderboardPlayerSnapshot>.Success(
                    createSnapshot(
                        internalLeaderboardId,
                        score: 0L,
                        rank: 0L,
                        hasScore: false,
                        status: LeaderboardPlayerSnapshotStatus.PlatformUnavailable)));
        }

        private sealed class AppleLeaderboardPlatformAdapter : ILeaderboardPlatformAdapter
        {
            public Task<CommonResult> InitializeAsync(CancellationToken ct)
            {
#if UNITY_IOS && !UNITY_EDITOR
                return Task.FromResult(CommonResult.Ok());
#else
                return Task.FromResult(CommonResult.Failure(
                    COMMON_ERROR_TYPE.LOGIN_UNSUPPORTED,
                    "Game Center adapter is not available on this platform."));
#endif
            }

            public async Task<CommonResult> ReportScoreAsync(string platformLeaderboardId, long score, CancellationToken ct)
            {
#if UNITY_IOS && !UNITY_EDITOR
                var auth = ensureAuthenticated();
                if (auth.IsFailure)
                    return auth;

                try
                {
                    var tcs = new TaskCompletionSource<bool>();
                    Social.ReportScore(score, platformLeaderboardId, success => tcs.TrySetResult(success));
                    var success = await tcs.Task;

                    return success
                        ? CommonResult.Ok()
                        : CommonResult.Failure(COMMON_ERROR_TYPE.COMMON_SERVER, "Game Center score report failed.");
                }
                catch (Exception ex)
                {
                    return CommonResult.Failure(COMMON_ERROR_TYPE.COMMON_SERVER, ex.Message);
                }
#else
                return CommonResult.Failure(COMMON_ERROR_TYPE.LOGIN_UNSUPPORTED, "Game Center adapter is not available on this platform.");
#endif
            }

            public async Task<CommonResult<LeaderboardPlayerSnapshot>> LoadPlayerSnapshotAsync(
                string platformLeaderboardId,
                string internalLeaderboardId,
                CancellationToken ct)
            {
#if UNITY_IOS && !UNITY_EDITOR
                var auth = ensureAuthenticated();
                if (auth.IsFailure)
                {
                    return CommonResult<LeaderboardPlayerSnapshot>.Success(
                        createSnapshot(
                            internalLeaderboardId,
                            score: 0L,
                            rank: 0L,
                            hasScore: false,
                            status: LeaderboardPlayerSnapshotStatus.NotLoggedIn));
                }

                try
                {
                    var loaded = await loadLocalUserScoreAsync(platformLeaderboardId, ct);
                    if (!loaded.success)
                    {
                        return CommonResult<LeaderboardPlayerSnapshot>.Success(
                            createSnapshot(
                                internalLeaderboardId,
                                score: 0L,
                                rank: 0L,
                                hasScore: false,
                                status: LeaderboardPlayerSnapshotStatus.Failed));
                    }

                    if (!loaded.hasScore)
                    {
                        return CommonResult<LeaderboardPlayerSnapshot>.Success(
                            createSnapshot(
                                internalLeaderboardId,
                                score: 0L,
                                rank: 0L,
                                hasScore: false,
                                status: LeaderboardPlayerSnapshotStatus.NoScore));
                    }

                    return CommonResult<LeaderboardPlayerSnapshot>.Success(
                        createSnapshot(
                            internalLeaderboardId,
                            loaded.score,
                            loaded.rank,
                            hasScore: true,
                            status: LeaderboardPlayerSnapshotStatus.Success));
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[{Tag}] Game Center snapshot load failed: {ex.Message}");
                    return CommonResult<LeaderboardPlayerSnapshot>.Success(
                        createSnapshot(
                            internalLeaderboardId,
                            score: 0L,
                            rank: 0L,
                            hasScore: false,
                            status: LeaderboardPlayerSnapshotStatus.Failed));
                }
#else
                return CommonResult<LeaderboardPlayerSnapshot>.Success(
                    createSnapshot(
                        internalLeaderboardId,
                        score: 0L,
                        rank: 0L,
                        hasScore: false,
                        status: LeaderboardPlayerSnapshotStatus.PlatformUnavailable));
#endif
            }

            private static CommonResult ensureAuthenticated()
            {
                if (Social.localUser != null && Social.localUser.authenticated)
                    return CommonResult.Ok();

                return CommonResult.Failure(COMMON_ERROR_TYPE.COMMON_AUTH, "Game Center authentication required.");
            }

            private static async Task<(bool success, bool hasScore, long score, long rank)> loadLocalUserScoreAsync(
                string platformLeaderboardId,
                CancellationToken ct)
            {
                ct.ThrowIfCancellationRequested();
                var leaderboard = Social.CreateLeaderboard();
                if (leaderboard == null)
                    return (false, false, 0L, 0L);

                leaderboard.id = platformLeaderboardId;
                leaderboard.userScope = UserScope.Global;
                leaderboard.timeScope = TimeScope.AllTime;
                leaderboard.range = new UnityEngine.SocialPlatforms.Range(1, 1);

                var tcs = new TaskCompletionSource<bool>();
                leaderboard.LoadScores(success => tcs.TrySetResult(success));

                using var cancelReg = ct.Register(() => tcs.TrySetCanceled(ct));
                var success = await tcs.Task;
                if (!success)
                    return (false, false, 0L, 0L);

                var localScore = leaderboard.localUserScore;
                if (localScore == null)
                    return (true, false, 0L, 0L);

                var score = Math.Max(0L, localScore.value);
                var rank = Math.Max(0L, localScore.rank);
                var hasScore = rank > 0 || score > 0;
                if (!hasScore)
                    return (true, false, 0L, 0L);

                return (true, true, score, rank);
            }
        }

        private sealed class GoogleLeaderboardPlatformAdapter : ILeaderboardPlatformAdapter
        {
            private bool _resolved;
            private object _platformInstance;
            private MethodInfo _reportScoreMethod;

            public Task<CommonResult> InitializeAsync(CancellationToken ct)
            {
#if UNITY_ANDROID && !UNITY_EDITOR
                if (_resolved)
                {
                    return Task.FromResult(_platformInstance != null
                        ? CommonResult.Ok()
                        : CommonResult.Failure(COMMON_ERROR_TYPE.COMMON_UNKNOWN, "Google Play Games plugin not found."));
                }

                _resolved = true;

                try
                {
                    var platformType = Type.GetType("GooglePlayGames.PlayGamesPlatform, Google.Play.Games");
                    if (platformType == null)
                    {
                        return Task.FromResult(CommonResult.Failure(
                            COMMON_ERROR_TYPE.COMMON_UNKNOWN,
                            "Google Play Games v2 plugin not found."));
                    }

                    _platformInstance = platformType.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static)?.GetValue(null);
                    if (_platformInstance == null)
                    {
                        var activate = platformType.GetMethod("Activate", BindingFlags.Public | BindingFlags.Static);
                        activate?.Invoke(null, null);
                        _platformInstance = platformType.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static)?.GetValue(null);
                    }

                    if (_platformInstance == null)
                    {
                        return Task.FromResult(CommonResult.Failure(
                            COMMON_ERROR_TYPE.COMMON_UNKNOWN,
                            "Google Play Games platform instance not available."));
                    }

                    _reportScoreMethod = findMethod(
                        platformType,
                        "ReportScore",
                        typeof(long), typeof(string), typeof(Action<bool>));

                    if (_reportScoreMethod == null)
                    {
                        return Task.FromResult(CommonResult.Failure(
                            COMMON_ERROR_TYPE.COMMON_UNKNOWN,
                            "Google Play Games API surface is missing required methods."));
                    }

                    return Task.FromResult(CommonResult.Ok());
                }
                catch (Exception ex)
                {
                    return Task.FromResult(CommonResult.Failure(COMMON_ERROR_TYPE.COMMON_SERVER, ex.Message));
                }
#else
                return Task.FromResult(CommonResult.Failure(
                    COMMON_ERROR_TYPE.LOGIN_UNSUPPORTED,
                    "Google adapter is not available on this platform."));
#endif
            }

            public async Task<CommonResult> ReportScoreAsync(string platformLeaderboardId, long score, CancellationToken ct)
            {
#if UNITY_ANDROID && !UNITY_EDITOR
                var init = await InitializeAsync(ct);
                if (init.IsFailure)
                    return init;

                var auth = ensureAuthenticated();
                if (auth.IsFailure)
                    return auth;

                try
                {
                    var tcs = new TaskCompletionSource<bool>();
                    var callback = new Action<bool>(success => tcs.TrySetResult(success));

                    _reportScoreMethod.Invoke(_platformInstance, new object[]
                    {
                        score,
                        platformLeaderboardId,
                        callback,
                    });

                    var success = await tcs.Task;
                    return success
                        ? CommonResult.Ok()
                        : CommonResult.Failure(COMMON_ERROR_TYPE.COMMON_SERVER, "GPGS score report failed.");
                }
                catch (Exception ex)
                {
                    return CommonResult.Failure(COMMON_ERROR_TYPE.COMMON_SERVER, ex.Message);
                }
#else
                return CommonResult.Failure(COMMON_ERROR_TYPE.LOGIN_UNSUPPORTED, "Google adapter is not available on this platform.");
#endif
            }

            public async Task<CommonResult<LeaderboardPlayerSnapshot>> LoadPlayerSnapshotAsync(
                string platformLeaderboardId,
                string internalLeaderboardId,
                CancellationToken ct)
            {
#if UNITY_ANDROID && !UNITY_EDITOR
                var init = await InitializeAsync(ct);
                if (init.IsFailure)
                {
                    return CommonResult<LeaderboardPlayerSnapshot>.Success(
                        createSnapshot(
                            internalLeaderboardId,
                            score: 0L,
                            rank: 0L,
                            hasScore: false,
                            status: LeaderboardPlayerSnapshotStatus.PlatformUnavailable));
                }

                var auth = ensureAuthenticated();
                if (auth.IsFailure)
                {
                    return CommonResult<LeaderboardPlayerSnapshot>.Success(
                        createSnapshot(
                            internalLeaderboardId,
                            score: 0L,
                            rank: 0L,
                            hasScore: false,
                            status: LeaderboardPlayerSnapshotStatus.NotLoggedIn));
                }

                try
                {
                    var loaded = await loadLocalUserScoreAsync(platformLeaderboardId, ct);
                    if (!loaded.success)
                    {
                        return CommonResult<LeaderboardPlayerSnapshot>.Success(
                            createSnapshot(
                                internalLeaderboardId,
                                score: 0L,
                                rank: 0L,
                                hasScore: false,
                                status: LeaderboardPlayerSnapshotStatus.Failed));
                    }

                    if (!loaded.hasScore)
                    {
                        return CommonResult<LeaderboardPlayerSnapshot>.Success(
                            createSnapshot(
                                internalLeaderboardId,
                                score: 0L,
                                rank: 0L,
                                hasScore: false,
                                status: LeaderboardPlayerSnapshotStatus.NoScore));
                    }

                    return CommonResult<LeaderboardPlayerSnapshot>.Success(
                        createSnapshot(
                            internalLeaderboardId,
                            loaded.score,
                            loaded.rank,
                            hasScore: true,
                            status: LeaderboardPlayerSnapshotStatus.Success));
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[{Tag}] GPGS snapshot load failed: {ex.Message}");
                    return CommonResult<LeaderboardPlayerSnapshot>.Success(
                        createSnapshot(
                            internalLeaderboardId,
                            score: 0L,
                            rank: 0L,
                            hasScore: false,
                            status: LeaderboardPlayerSnapshotStatus.Failed));
                }
#else
                return CommonResult<LeaderboardPlayerSnapshot>.Success(
                    createSnapshot(
                        internalLeaderboardId,
                        score: 0L,
                        rank: 0L,
                        hasScore: false,
                        status: LeaderboardPlayerSnapshotStatus.PlatformUnavailable));
#endif
            }

            private static MethodInfo findMethod(Type type, string methodName, params Type[] paramTypes)
            {
                var methods = type.GetMethods(BindingFlags.Public | BindingFlags.Instance);
                for (var i = 0; i < methods.Length; i++)
                {
                    var m = methods[i];
                    if (!string.Equals(m.Name, methodName, StringComparison.Ordinal))
                        continue;

                    var ps = m.GetParameters();
                    if (ps.Length != paramTypes.Length)
                        continue;

                    var matched = true;
                    for (var p = 0; p < ps.Length; p++)
                    {
                        if (ps[p].ParameterType != paramTypes[p])
                        {
                            matched = false;
                            break;
                        }
                    }

                    if (matched)
                        return m;
                }

                return null;
            }

            private static CommonResult ensureAuthenticated()
            {
                if (Social.localUser != null && Social.localUser.authenticated)
                    return CommonResult.Ok();

                return CommonResult.Failure(COMMON_ERROR_TYPE.COMMON_AUTH, "Google Play Games authentication required.");
            }

            private static async Task<(bool success, bool hasScore, long score, long rank)> loadLocalUserScoreAsync(
                string platformLeaderboardId,
                CancellationToken ct)
            {
                ct.ThrowIfCancellationRequested();
                var leaderboard = Social.CreateLeaderboard();
                if (leaderboard == null)
                    return (false, false, 0L, 0L);

                leaderboard.id = platformLeaderboardId;
                leaderboard.userScope = UserScope.Global;
                leaderboard.timeScope = TimeScope.AllTime;
                leaderboard.range = new UnityEngine.SocialPlatforms.Range(1, 1);

                var tcs = new TaskCompletionSource<bool>();
                leaderboard.LoadScores(success => tcs.TrySetResult(success));

                using var cancelReg = ct.Register(() => tcs.TrySetCanceled(ct));
                var success = await tcs.Task;
                if (!success)
                    return (false, false, 0L, 0L);

                var localScore = leaderboard.localUserScore;
                if (localScore == null)
                    return (true, false, 0L, 0L);

                var score = Math.Max(0L, localScore.value);
                var rank = Math.Max(0L, localScore.rank);
                var hasScore = rank > 0 || score > 0;
                if (!hasScore)
                    return (true, false, 0L, 0L);

                return (true, true, score, rank);
            }
        }
    }
}
