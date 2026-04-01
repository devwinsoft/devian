using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Devian.Domain.Common;
using Devian.Domain.Game;

namespace Devian
{
    [Serializable]
    public enum AttendRuntimeState
    {
        NONE = 0,
        WAIT = 1,
        CLAIMABLE = 2,
        CLAIMED = 3,
    }

    [Serializable]
    public sealed class AttendRuntime
    {
        public int Day { get; }
        public string AttendId { get; }
        public string RewardGroupId { get; }
        public bool IsConfigured { get; }
        public AttendRuntimeState State { get; }
        public long ClaimedAtUtcMs { get; }

        public AttendRuntime(
            int day,
            string attendId,
            string rewardGroupId,
            bool isConfigured,
            AttendRuntimeState state,
            long claimedAtUtcMs)
        {
            Day = day;
            AttendId = attendId ?? string.Empty;
            RewardGroupId = rewardGroupId ?? string.Empty;
            IsConfigured = isConfigured;
            State = state;
            ClaimedAtUtcMs = claimedAtUtcMs > 0L ? claimedAtUtcMs : 0L;
        }
    }

    public sealed class AttendManager : CompoSingleton<AttendManager>
    {
        const int MaxAttendDay = 7;
        const int CompletedAttendDay = MaxAttendDay + 1;
        const long ResetAfterClaimMs = 72L * 60L * 60L * 1000L;
        const long DayMs = 24L * 60L * 60L * 1000L;

        readonly AttendStorage _storage = new();
        readonly List<ATTEND> _activeRows = new();
        readonly List<AttendRuntime> _runtimes = new(MaxAttendDay);
        readonly Dictionary<string, ATTEND> _rowById = new(StringComparer.Ordinal);
        readonly Dictionary<int, ATTEND> _rowByDay = new();
        readonly Dictionary<string, AttendRuntime> _runtimeById = new(StringComparer.Ordinal);
        readonly Dictionary<int, AttendRuntime> _runtimeByDay = new();

        bool _initialized;

        public AttendStorage Storage => _storage;
        public bool IsInitialized => _initialized;
        public IReadOnlyList<ATTEND> ActiveRows => _activeRows;
        public IReadOnlyList<AttendRuntime> Runtimes => _runtimes;

        public Task<CommonResult> InitializeAsync(CancellationToken ct = default)
        {
            _ = ct;

            if (!RemoteDataManager.TryGetServerNowUtcMs(out var serverNowUtcMs))
            {
                return Task.FromResult(CommonResult.Failure(
                    COMMON_ERROR_TYPE.COMMON_SERVER,
                    "Server time is unavailable."));
            }

            rebuildRowCache();
            refreshState(serverNowUtcMs);
            _initialized = true;

            return Task.FromResult(CommonResult.Ok());
        }

        public void RefreshCycle()
        {
            if (!_initialized)
                return;

            if (!RemoteDataManager.TryGetServerNowUtcMs(out var serverNowUtcMs))
                return;

            rebuildRowCache();
            refreshState(serverNowUtcMs);
        }

        public int GetCurrentCycleDay()
        {
            if (!_initialized)
                return 0;

            if (!RemoteDataManager.TryGetServerNowUtcMs(out var serverNowUtcMs))
                return 0;

            rebuildRowCache();
            refreshState(serverNowUtcMs);
            return clampDayForDisplay(_storage.nextAttendDay);
        }

        public AttendRuntimeState GetRuntimeState(string attendId)
        {
            if (!_initialized || string.IsNullOrWhiteSpace(attendId))
                return AttendRuntimeState.NONE;

            if (!RemoteDataManager.TryGetServerNowUtcMs(out var serverNowUtcMs))
                return AttendRuntimeState.NONE;

            rebuildRowCache();
            refreshState(serverNowUtcMs);

            return _runtimeById.TryGetValue(attendId.Trim(), out var runtime) && runtime != null
                ? runtime.State
                : AttendRuntimeState.NONE;
        }

        public AttendRuntime GetRuntime(int day)
        {
            if (!_initialized || day < 1 || day > MaxAttendDay)
                return null;

            if (!RemoteDataManager.TryGetServerNowUtcMs(out var serverNowUtcMs))
                return null;

            rebuildRowCache();
            refreshState(serverNowUtcMs);

            return _runtimeByDay.TryGetValue(day, out var runtime) && runtime != null
                ? runtime
                : null;
        }

        public bool IsClaimed(string attendId)
        {
            return _storage.IsClaimed(attendId);
        }

        public bool IsClaimable(string attendId)
        {
            if (!_initialized || string.IsNullOrWhiteSpace(attendId))
                return false;

            if (!RemoteDataManager.TryGetServerNowUtcMs(out var serverNowUtcMs))
                return false;

            rebuildRowCache();
            refreshState(serverNowUtcMs);

            if (!_rowById.TryGetValue(attendId.Trim(), out var row) || row == null)
                return false;

            return isRowClaimable(row, attendId.Trim(), serverNowUtcMs);
        }

        public async Task<CommonResult<RewardData[]>> ClaimAsync(string attendId, CancellationToken ct = default)
        {
            if (!_initialized)
                return CommonResult<RewardData[]>.Failure(COMMON_ERROR_TYPE.SAVEDATA_SYNC_REQUIRED, "AttendManager is not initialized.");

            if (string.IsNullOrWhiteSpace(attendId))
                return CommonResult<RewardData[]>.Failure(COMMON_ERROR_TYPE.COMMON_INVALID_ARGUMENT, "attend_id is empty.");

            ct.ThrowIfCancellationRequested();

            if (!RemoteDataManager.TryGetServerNowUtcMs(out var serverNowUtcMs))
                return CommonResult<RewardData[]>.Failure(COMMON_ERROR_TYPE.COMMON_SERVER, "Server time is unavailable.");

            rebuildRowCache();
            refreshState(serverNowUtcMs);

            if (_rowByDay.Count <= 0)
            {
                return CommonResult<RewardData[]>.Failure(
                    COMMON_ERROR_TYPE.COMMON_INVALID_ARGUMENT,
                    "ATTEND table is empty or contains no active rows.");
            }

            var key = attendId.Trim();
            if (!_rowById.TryGetValue(key, out var row) || row == null)
                return CommonResult<RewardData[]>.Failure(COMMON_ERROR_TYPE.COMMON_INVALID_ARGUMENT, $"Attend row not found: {key}");

            if (isClaimedToday(serverNowUtcMs))
            {
                return CommonResult<RewardData[]>.Failure(
                    COMMON_ERROR_TYPE.COMMON_INVALID_ARGUMENT,
                    "Attend reward has already been claimed today.");
            }

            if (_storage.nextAttendDay > MaxAttendDay)
            {
                return CommonResult<RewardData[]>.Failure(
                    COMMON_ERROR_TYPE.COMMON_INVALID_ARGUMENT,
                    "Attend cycle is completed. Wait for next day reset.");
            }

            if (!isRowClaimable(row, key, serverNowUtcMs))
            {
                if (row.day != _storage.nextAttendDay)
                {
                    return CommonResult<RewardData[]>.Failure(
                        COMMON_ERROR_TYPE.COMMON_INVALID_ARGUMENT,
                        $"Attend day mismatch: requested={row.day}, expected={_storage.nextAttendDay}");
                }

                return CommonResult<RewardData[]>.Failure(
                    COMMON_ERROR_TYPE.COMMON_INVALID_ARGUMENT,
                    $"Attend is not claimable: {row.attend_id}");
            }

            var apply = RewardManager.Instance.ApplyRewardGroup(row.reward_group_id);
            if (apply.IsFailure)
                return CommonResult<RewardData[]>.Failure(apply.Error!);

            _storage.SetClaimed(row.attend_id, serverNowUtcMs);
            _storage.nextAttendDay = row.day >= MaxAttendDay
                ? CompletedAttendDay
                : row.day + 1;
            _storage.MarkLogin(serverNowUtcMs);
            rebuildRuntimeCache(serverNowUtcMs);

            var save = await SaveDataManager.Instance.SaveGameStorageAsync(true, ct);
            if (save.IsFailure)
                return CommonResult<RewardData[]>.Failure(save.Error!);

            return CommonResult<RewardData[]>.Success(apply.Value.AppliedRewards ?? Array.Empty<RewardData>());
        }

        public void ClearStorage()
        {
            _storage.Clear();
            _runtimes.Clear();
            _runtimeById.Clear();
            _runtimeByDay.Clear();
            _initialized = false;
        }

        void rebuildRowCache()
        {
            _activeRows.Clear();
            _rowById.Clear();
            _rowByDay.Clear();

            var rows = TB_ATTEND.GetAll();
            for (var i = 0; i < rows.Count; i++)
            {
                var row = rows[i];
                if (!isValidActiveRow(row))
                    continue;

                _activeRows.Add(row);
            }

            _activeRows.Sort(compareRowByDayThenId);

            for (var i = 0; i < _activeRows.Count; i++)
            {
                var row = _activeRows[i];
                if (!_rowByDay.ContainsKey(row.day))
                    _rowByDay.Add(row.day, row);

                if (!_rowById.ContainsKey(row.attend_id))
                    _rowById.Add(row.attend_id, row);
            }
        }

        void refreshState(long serverNowUtcMs)
        {
            normalizeNextAttendDay();

            if (shouldResetForMissingInfo()
                || shouldResetForClaimGap(serverNowUtcMs)
                || shouldResetAfterDaySevenClaim(serverNowUtcMs))
            {
                _storage.ResetCycle(toUtcDayStart(serverNowUtcMs));
            }

            if (_storage.cycleStartUtcMs <= 0L)
                _storage.cycleStartUtcMs = toUtcDayStart(serverNowUtcMs);

            _storage.MarkLogin(serverNowUtcMs);
            rebuildRuntimeCache(serverNowUtcMs);
        }

        void rebuildRuntimeCache(long serverNowUtcMs)
        {
            _runtimes.Clear();
            _runtimeById.Clear();
            _runtimeByDay.Clear();

            for (var day = 1; day <= MaxAttendDay; day++)
            {
                _rowByDay.TryGetValue(day, out var row);
                var runtime = createRuntime(day, row, serverNowUtcMs);
                _runtimes.Add(runtime);
                _runtimeByDay[day] = runtime;

                if (!runtime.IsConfigured || string.IsNullOrEmpty(runtime.AttendId))
                    continue;

                if (!_runtimeById.ContainsKey(runtime.AttendId))
                    _runtimeById.Add(runtime.AttendId, runtime);
            }
        }

        AttendRuntime createRuntime(int day, ATTEND row, long serverNowUtcMs)
        {
            if (row == null)
                return new AttendRuntime(day, string.Empty, string.Empty, false, AttendRuntimeState.WAIT, 0L);

            var attendId = (row.attend_id ?? string.Empty).Trim();
            var rewardGroupId = (row.reward_group_id ?? string.Empty).Trim();
            var isConfigured = !string.IsNullOrEmpty(attendId) && !string.IsNullOrEmpty(rewardGroupId);
            var claimedAtUtcMs = 0L;
            var state = AttendRuntimeState.WAIT;

            if (isConfigured && _storage.TryGetClaimedAtUtcMs(attendId, out claimedAtUtcMs))
            {
                state = AttendRuntimeState.CLAIMED;
            }
            else if (isConfigured && isRowClaimable(row, attendId, serverNowUtcMs))
            {
                state = AttendRuntimeState.CLAIMABLE;
            }

            return new AttendRuntime(day, attendId, rewardGroupId, isConfigured, state, claimedAtUtcMs);
        }

        void normalizeNextAttendDay()
        {
            if (_storage.nextAttendDay < 1)
                _storage.nextAttendDay = 1;
            else if (_storage.nextAttendDay > CompletedAttendDay)
                _storage.nextAttendDay = CompletedAttendDay;
        }

        bool shouldResetForMissingInfo()
        {
            return _storage.lastClaimUtcMs <= 0L
                && _storage.claimedAttendUtcMs.Count <= 0;
        }

        bool shouldResetForClaimGap(long serverNowUtcMs)
        {
            if (_storage.lastClaimUtcMs <= 0L)
                return false;

            if (serverNowUtcMs <= _storage.lastClaimUtcMs)
                return false;

            return (serverNowUtcMs - _storage.lastClaimUtcMs) >= ResetAfterClaimMs;
        }

        bool shouldResetAfterDaySevenClaim(long serverNowUtcMs)
        {
            if (_storage.nextAttendDay <= MaxAttendDay)
                return false;

            if (_storage.lastClaimUtcMs <= 0L)
                return false;

            return hasUtcDayChanged(_storage.lastClaimUtcMs, serverNowUtcMs);
        }

        bool isRowClaimable(ATTEND row, string rowKey, long serverNowUtcMs)
        {
            if (row == null || string.IsNullOrEmpty(rowKey))
                return false;

            if (_storage.nextAttendDay <= 0 || _storage.nextAttendDay > MaxAttendDay)
                return false;

            if (row.day != _storage.nextAttendDay)
                return false;

            if (isClaimedToday(serverNowUtcMs))
                return false;

            if (!_rowByDay.TryGetValue(_storage.nextAttendDay, out var targetRow)
                || targetRow == null)
            {
                return false;
            }

            if (!string.Equals(targetRow.attend_id, row.attend_id, StringComparison.Ordinal))
                return false;

            if (!string.Equals(row.attend_id, rowKey, StringComparison.Ordinal))
                return false;

            if (_storage.IsClaimed(row.attend_id))
                return false;

            return true;
        }

        bool isClaimedToday(long serverNowUtcMs)
        {
            if (_storage.lastClaimUtcMs <= 0L)
                return false;

            return toUtcDayStart(_storage.lastClaimUtcMs) == toUtcDayStart(serverNowUtcMs);
        }

        static bool hasUtcDayChanged(long fromUtcMs, long toUtcMs)
        {
            if (fromUtcMs <= 0L || toUtcMs <= 0L)
                return false;

            return toUtcDayStart(toUtcMs) > toUtcDayStart(fromUtcMs);
        }

        static int clampDayForDisplay(int day)
        {
            if (day <= 1)
                return 1;

            if (day >= MaxAttendDay)
                return MaxAttendDay;

            return day;
        }

        static bool isValidActiveRow(ATTEND row)
        {
            if (row == null || !row.is_active)
                return false;

            if (string.IsNullOrWhiteSpace(row.attend_id))
                return false;

            if (row.day <= 0 || row.day > MaxAttendDay)
                return false;

            if (string.IsNullOrWhiteSpace(row.reward_group_id))
                return false;

            return true;
        }

        static int compareRowByDayThenId(ATTEND x, ATTEND y)
        {
            if (ReferenceEquals(x, y))
                return 0;

            if (x == null)
                return 1;

            if (y == null)
                return -1;

            var dayCompare = x.day.CompareTo(y.day);
            if (dayCompare != 0)
                return dayCompare;

            return string.Compare(x.attend_id, y.attend_id, StringComparison.Ordinal);
        }

        static long toUtcDayStart(long utcMs)
        {
            if (utcMs <= 0L)
                return 0L;

            return utcMs - (utcMs % DayMs);
        }
    }
}
