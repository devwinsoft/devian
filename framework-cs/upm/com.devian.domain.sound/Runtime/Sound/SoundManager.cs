// SSOT: skills/devian-unity/30-unity-components/17-sound-manager/SKILL.md

#nullable enable

using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Devian
{
    /// <summary>
    /// 테이블 기반 사운드 재생/풀/채널/쿨타임 관리자.
    /// - 테이블(TB_SOUND) 기반으로만 식별
    /// - row_id가 PK, sound_id는 논리 그룹 키 (weight 기반 랜덤 선택)
    /// - 채널/풀 책임은 SoundChannel에 위임
    /// - SerializeField 의존 없이 런타임 생성
    /// - Voice 채널 로딩은 VoiceManager가 담당 (언어 파라미터 없음)
    /// - PlaySound는 SoundRuntimeId를 반환하고, 모든 제어는 runtime_id 기반
    /// </summary>
    public sealed class SoundManager : AutoSingleton<SoundManager>
    {
        // ====================================================================
        // Sound Table Registry
        // ====================================================================

        /// <summary>
        /// sound_id로 후보 rows를 조회하는 델리게이트 (Play용).
        /// 동일 sound_id에 여러 row가 있을 수 있다.
        /// </summary>
        public Func<string, IReadOnlyList<ISoundRow>>? GetSoundRowsBySoundId { get; set; }

        /// <summary>
        /// key(게임 로딩 그룹)로 rows를 조회하는 델리게이트 (Load/Unload용).
        /// </summary>
        public Func<string, IEnumerable<ISoundRow>>? GetSoundRowsByKey { get; set; }

        // ====================================================================
        // Channels
        // ====================================================================

        private readonly Dictionary<SoundChannelType, SoundChannel> _channels = new();

        protected override void Awake()
        {
            base.Awake();
            _initializeChannels();
        }

        private void _initializeChannels()
        {
            for (int i = 0; i < (int)SoundChannelType.Max; i++)
            {
                var type = (SoundChannelType)i;
                var channelGo = new GameObject($"Channel_{type}");
                channelGo.transform.SetParent(transform);
                var channel = channelGo.AddComponent<SoundChannel>();
                channel.Initialize(type, poolSize: type == SoundChannelType.Bgm ? 2 : 8);
                channel.OnPlayFinished = _onPlayFinished;
                _channels[type] = channel;
            }
        }

        // ====================================================================
        // Runtime ID Management (단일 SSOT)
        // ====================================================================

        private int _nextRuntimeId = 1;

        // runtime_id → channel 매핑 (제어 시 채널 탐색용)
        private readonly Dictionary<int, SoundChannelType> _channelByRuntimeId = new();

        /// <summary>
        /// 새 runtime_id를 발급한다.
        /// </summary>
        private SoundRuntimeId _allocateRuntimeId()
        {
            var id = new SoundRuntimeId(_nextRuntimeId);
            _nextRuntimeId++;

            // 오버플로우 방지 (극히 드문 경우)
            if (_nextRuntimeId <= 0)
            {
                _nextRuntimeId = 1;
            }

            return id;
        }

        /// <summary>
        /// 재생 종료 시 호출되는 콜백.
        /// </summary>
        private void _onPlayFinished(SoundRuntimeId runtimeId)
        {
            _channelByRuntimeId.Remove(runtimeId.Value);
        }

        // ====================================================================
        // Loaded Clips Cache (row_id 기준)
        // ====================================================================

        // row_id → AudioClip
        private readonly Dictionary<int, AudioClip> _clipCacheByRowId = new();

        // gameKey → 로드된 row_id 목록 (언로드용)
        private readonly Dictionary<string, List<int>> _loadedRowIdsByGameKey = new();

        private readonly HashSet<string> _loadedBundleKeys = new();
        private readonly HashSet<string> _loadedGameKeys = new();

        // ====================================================================
        // Loading Policy (Sound Only - Voice 제외)
        // ====================================================================

        /// <summary>
        /// 게임 로딩 그룹(key) 기준으로 사운드를 로드한다.
        /// Voice 채널은 로드하지 않는다 (VoiceManager.LoadByGroupKeyAsync 사용).
        /// </summary>
        public IEnumerator LoadByKeyAsync(string gameKey, Action<string>? onError = null)
        {
            if (_loadedGameKeys.Contains(gameKey))
            {
                yield break;
            }

            if (GetSoundRowsByKey == null)
            {
                onError?.Invoke("[SoundManager] GetSoundRowsByKey delegate not set.");
                yield break;
            }

            var rows = GetSoundRowsByKey(gameKey);
            if (rows == null)
            {
                _loadedGameKeys.Add(gameKey);
                yield break;
            }

            // bundle_key별로 그룹화 (Voice 채널 제외)
            var bundleGroups = new Dictionary<string, List<ISoundRow>>();
            var loadedRowIds = new List<int>();

            foreach (var row in rows)
            {
                if (row == null) continue;
                if (row.source != SoundSourceType.Bundle) continue;

                // Voice 채널 제외 (Voice는 VoiceManager가 담당)
                if (string.Equals(row.channel, "Voice", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (!bundleGroups.TryGetValue(row.bundle_key, out var list))
                {
                    list = new List<ISoundRow>();
                    bundleGroups[row.bundle_key] = list;
                }
                list.Add(row);
                loadedRowIds.Add(row.row_id);
            }

            // 번들 로드 (언어 무관)
            foreach (var kvp in bundleGroups)
            {
                var bundleKey = kvp.Key;
                if (_loadedBundleKeys.Contains(bundleKey)) continue;

                yield return _loadBundleAsync(bundleKey, kvp.Value, onError);
                _loadedBundleKeys.Add(bundleKey);
            }

            // 언로드용 row_id 목록 저장
            _loadedRowIdsByGameKey[gameKey] = loadedRowIds;
            _loadedGameKeys.Add(gameKey);
        }

        /// <summary>
        /// 게임 로딩 그룹(key) 기준으로 사운드를 언로드한다.
        /// </summary>
        public void UnloadByKey(string gameKey)
        {
            if (!_loadedGameKeys.Contains(gameKey)) return;

            // 해당 key의 row_id들을 캐시에서 제거
            if (_loadedRowIdsByGameKey.TryGetValue(gameKey, out var rowIds))
            {
                foreach (var rowId in rowIds)
                {
                    _clipCacheByRowId.Remove(rowId);
                }
                _loadedRowIdsByGameKey.Remove(gameKey);
            }

            _loadedGameKeys.Remove(gameKey);

            // Note: bundle_key 단위 언로드는 여러 gameKey가 공유할 수 있으므로
            // 여기서는 clip 캐시만 제거. 실제 Addressables Release는 별도 정책 필요.
        }

        // ====================================================================
        // Voice Loading Helper (VoiceManager 전용, internal)
        // ====================================================================

        /// <summary>
        /// VoiceManager가 호출하는 Voice clip 로드 헬퍼.
        /// Resolve된 sound_id 집합을 받아 해당 voice clip만 로드한다.
        /// </summary>
        internal IEnumerator _loadVoiceBySoundIdsAsync(
            string voiceGroupKey,
            IEnumerable<string> soundIds,
            SystemLanguage language,
            SystemLanguage fallbackLanguage,
            Action<string>? onError = null)
        {
            if (_loadedGameKeys.Contains(voiceGroupKey))
            {
                yield break;
            }

            if (GetSoundRowsBySoundId == null)
            {
                onError?.Invoke("[SoundManager] GetSoundRowsBySoundId delegate not set.");
                yield break;
            }

            // sound_id별로 Voice 채널 rows 수집
            var bundleGroups = new Dictionary<string, List<ISoundRow>>();
            var loadedRowIds = new List<int>();

            foreach (var soundId in soundIds)
            {
                if (string.IsNullOrEmpty(soundId)) continue;

                var candidates = GetSoundRowsBySoundId(soundId);
                if (candidates == null || candidates.Count == 0) continue;

                foreach (var row in candidates)
                {
                    if (row == null) continue;
                    if (row.source != SoundSourceType.Bundle) continue;

                    // Voice 채널만 수집
                    if (!string.Equals(row.channel, "Voice", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    if (!bundleGroups.TryGetValue(row.bundle_key, out var list))
                    {
                        list = new List<ISoundRow>();
                        bundleGroups[row.bundle_key] = list;
                    }

                    // 중복 row 방지
                    bool exists = false;
                    foreach (var existing in list)
                    {
                        if (existing.row_id == row.row_id)
                        {
                            exists = true;
                            break;
                        }
                    }
                    if (!exists)
                    {
                        list.Add(row);
                        loadedRowIds.Add(row.row_id);
                    }
                }
            }

            // Voice 번들 로드 (언어 라벨 교집합)
            foreach (var kvp in bundleGroups)
            {
                var bundleKey = kvp.Key;
                var voiceBundleKey = bundleKey;

                if (!_loadedBundleKeys.Contains(voiceBundleKey))
                {
                    yield return _loadVoiceBundleAsync(bundleKey, kvp.Value, language, fallbackLanguage, onError);
                    _loadedBundleKeys.Add(voiceBundleKey);
                }
                else
                {
                    // 번들은 이미 로드됨, clip 캐시만 등록
                    _populateClipCache(kvp.Value, onError);
                }
            }

            // 언로드용 row_id 목록 저장
            _loadedRowIdsByGameKey[voiceGroupKey] = loadedRowIds;
            _loadedGameKeys.Add(voiceGroupKey);
        }

        private IEnumerator _loadBundleAsync(string bundleKey, List<ISoundRow> rows, Action<string>? onError)
        {
            yield return AssetManager.LoadBundleAssets<AudioClip>(bundleKey);
            _populateClipCache(rows, onError);
        }

        private IEnumerator _loadVoiceBundleAsync(
            string bundleKey,
            List<ISoundRow> rows,
            SystemLanguage language,
            SystemLanguage fallbackLanguage,
            Action<string>? onError)
        {
            if (language != SystemLanguage.Unknown)
            {
                yield return AssetManager.LoadBundleAssets<AudioClip>(bundleKey, language);
            }
            else
            {
                yield return AssetManager.LoadBundleAssets<AudioClip>(bundleKey);
            }

            _populateClipCache(rows, onError);
        }

        private void _populateClipCache(List<ISoundRow> rows, Action<string>? onError)
        {
            int loadedCount = 0;
            foreach (var row in rows)
            {
                var assetName = _extractAssetName(row.path);
                var clip = AssetManager.GetAsset<AudioClip>(assetName);
                if (clip != null)
                {
                    _clipCacheByRowId[row.row_id] = clip;
                    loadedCount++;
                }
                else
                {
                    onError?.Invoke($"[SoundManager] AudioClip not found for row_id '{row.row_id}', path '{row.path}'.");
                }
            }

            if (rows.Count > 0 && loadedCount == 0)
            {
                onError?.Invoke($"[SoundManager] No AudioClips loaded for bundle (expected {rows.Count} clips).");
            }
        }

        // ====================================================================
        // Play API (runtime_id 기반)
        // ====================================================================

        /// <summary>
        /// sound_id로 2D 사운드를 재생한다.
        /// 동일 sound_id의 후보 rows 중 weight 기반 랜덤으로 1개를 선택하여 재생한다.
        /// 쿨타임은 논리키(sound_id) 단위로 적용된다.
        /// </summary>
        /// <returns>재생 성공 시 유효한 SoundRuntimeId, 실패 시 SoundRuntimeId.Invalid</returns>
        public SoundRuntimeId PlaySound(
            string soundId,
            float volume = 1f,
            float pitch = 1f,
            int groupId = 0,
            string? channelOverride = null)
        {
            return _playInternal(soundId, volume, pitch, groupId, null, channelOverride);
        }

        /// <summary>
        /// sound_id로 3D 사운드를 재생한다.
        /// 위치 정보가 필수이며, 3D 파라미터(distance_near, distance_far)는 row에서 가져온다.
        /// </summary>
        /// <returns>재생 성공 시 유효한 SoundRuntimeId, 실패 시 SoundRuntimeId.Invalid</returns>
        public SoundRuntimeId PlaySound3D(
            string soundId,
            Vector3 position,
            float volume = 1f,
            float pitch = 1f,
            int groupId = 0,
            string? channelOverride = null)
        {
            return _playInternal(soundId, volume, pitch, groupId, position, channelOverride);
        }

        /// <summary>
        /// 내부 재생 구현 (2D/3D 공통).
        /// </summary>
        private SoundRuntimeId _playInternal(
            string soundId,
            float volume,
            float pitch,
            int groupId,
            Vector3? position,
            string? channelOverride)
        {
            if (GetSoundRowsBySoundId == null)
            {
                Log.Warn("[SoundManager] GetSoundRowsBySoundId delegate not set.");
                return SoundRuntimeId.Invalid;
            }

            // 1. 후보 rows 조회
            var candidates = GetSoundRowsBySoundId(soundId);
            if (candidates == null || candidates.Count == 0)
            {
                Log.Warn($"[SoundManager] Sound not found: {soundId}");
                return SoundRuntimeId.Invalid;
            }

            // 2. 캐시에 로드된 row만 필터링
            var loadedCandidates = new List<ISoundRow>();
            foreach (var row in candidates)
            {
                if (_clipCacheByRowId.ContainsKey(row.row_id))
                {
                    loadedCandidates.Add(row);
                }
            }

            if (loadedCandidates.Count == 0)
            {
                Log.Warn($"[SoundManager] AudioClip not loaded for sound_id: {soundId}");
                return SoundRuntimeId.Invalid;
            }

            // 3. weight 기반 랜덤 선택
            var selectedRow = _selectByWeight(loadedCandidates);
            if (selectedRow == null)
            {
                Log.Warn($"[SoundManager] Failed to select row for: {soundId}");
                return SoundRuntimeId.Invalid;
            }

            // 4. 클립 조회
            if (!_clipCacheByRowId.TryGetValue(selectedRow.row_id, out var clip))
            {
                Log.Warn($"[SoundManager] AudioClip cache miss for row_id: {selectedRow.row_id}");
                return SoundRuntimeId.Invalid;
            }

            // 5. 채널 결정
            var channelName = channelOverride ?? selectedRow.channel;
            if (!_tryParseChannel(channelName, out var channelType))
            {
                channelType = SoundChannelType.Effect;
            }

            if (!_channels.TryGetValue(channelType, out var channel))
            {
                Log.Warn($"[SoundManager] Channel not found: {channelType}");
                return SoundRuntimeId.Invalid;
            }

            // 6. 볼륨/피치 계산 (BaseAudioManager 로직)
            var effectiveVolume = volume * selectedRow.volume_scale;
            var effectivePitch = pitch;
            if (selectedRow.pitch_min > 0f && selectedRow.pitch_max > 0f && selectedRow.pitch_min < selectedRow.pitch_max)
            {
                effectivePitch = UnityEngine.Random.Range(selectedRow.pitch_min, selectedRow.pitch_max);
            }

            // 7. 3D 판정 (position이 주어지면 3D, 아니면 2D)
            var is3d = position.HasValue;

            // 8. runtime_id 발급
            var runtimeId = _allocateRuntimeId();

            // 9. 채널에 재생 요청 (distance_near/distance_far 사용)
            var success = channel.PlayWithRuntimeId(
                runtimeId,
                soundId,  // 쿨타임 공유를 위해 sound_id 전달
                selectedRow.row_id,
                clip,
                effectiveVolume,
                selectedRow.loop,
                selectedRow.cooltime,
                fadeInSeconds: 0f,
                fadeOutSeconds: 0f,
                waitSeconds: 0f,
                groupId,
                effectivePitch,
                is3d,
                position,
                selectedRow.distance_near,
                selectedRow.distance_far
            );

            if (!success)
            {
                // 쿨타임 등으로 재생 실패
                return SoundRuntimeId.Invalid;
            }

            // 10. channel 매핑 등록
            _channelByRuntimeId[runtimeId.Value] = channelType;

            return runtimeId;
        }

        /// <summary>
        /// TryPlaySound 버전 (out 파라미터).
        /// </summary>
        public bool TryPlaySound(
            string soundId,
            out SoundRuntimeId runtimeId,
            float volume = 1f,
            float pitch = 1f,
            int groupId = 0,
            string? channelOverride = null)
        {
            runtimeId = PlaySound(soundId, volume, pitch, groupId, channelOverride);
            return runtimeId.IsValid;
        }

        /// <summary>
        /// TryPlaySound3D 버전 (out 파라미터).
        /// </summary>
        public bool TryPlaySound3D(
            string soundId,
            Vector3 position,
            out SoundRuntimeId runtimeId,
            float volume = 1f,
            float pitch = 1f,
            int groupId = 0,
            string? channelOverride = null)
        {
            runtimeId = PlaySound3D(soundId, position, volume, pitch, groupId, channelOverride);
            return runtimeId.IsValid;
        }

        // ====================================================================
        // Control API (runtime_id 기반)
        // ====================================================================

        /// <summary>
        /// runtime_id로 재생을 정지한다.
        /// </summary>
        public bool StopSound(SoundRuntimeId runtimeId)
        {
            if (!runtimeId.IsValid) return false;

            if (!_channelByRuntimeId.TryGetValue(runtimeId.Value, out var channelType))
            {
                return false;
            }

            if (!_channels.TryGetValue(channelType, out var channel))
            {
                return false;
            }

            return channel.StopByRuntimeId(runtimeId);
        }

        /// <summary>
        /// runtime_id로 재생을 일시정지한다.
        /// </summary>
        public bool PauseSound(SoundRuntimeId runtimeId)
        {
            if (!runtimeId.IsValid) return false;

            if (!_channelByRuntimeId.TryGetValue(runtimeId.Value, out var channelType))
            {
                return false;
            }

            if (!_channels.TryGetValue(channelType, out var channel))
            {
                return false;
            }

            return channel.PauseByRuntimeId(runtimeId);
        }

        /// <summary>
        /// runtime_id로 일시정지를 해제한다.
        /// </summary>
        public bool ResumeSound(SoundRuntimeId runtimeId)
        {
            if (!runtimeId.IsValid) return false;

            if (!_channelByRuntimeId.TryGetValue(runtimeId.Value, out var channelType))
            {
                return false;
            }

            if (!_channels.TryGetValue(channelType, out var channel))
            {
                return false;
            }

            return channel.ResumeByRuntimeId(runtimeId);
        }

        /// <summary>
        /// runtime_id로 볼륨을 설정한다.
        /// </summary>
        public bool SetSoundVolume(SoundRuntimeId runtimeId, float volume)
        {
            if (!runtimeId.IsValid) return false;

            if (!_channelByRuntimeId.TryGetValue(runtimeId.Value, out var channelType))
            {
                return false;
            }

            if (!_channels.TryGetValue(channelType, out var channel))
            {
                return false;
            }

            return channel.SetVolumeByRuntimeId(runtimeId, volume);
        }

        /// <summary>
        /// runtime_id로 피치를 설정한다.
        /// </summary>
        public bool SetSoundPitch(SoundRuntimeId runtimeId, float pitch)
        {
            if (!runtimeId.IsValid) return false;

            if (!_channelByRuntimeId.TryGetValue(runtimeId.Value, out var channelType))
            {
                return false;
            }

            if (!_channels.TryGetValue(channelType, out var channel))
            {
                return false;
            }

            return channel.SetPitchByRuntimeId(runtimeId, pitch);
        }

        /// <summary>
        /// runtime_id로 재생 중인지 확인한다.
        /// </summary>
        public bool IsPlaying(SoundRuntimeId runtimeId)
        {
            if (!runtimeId.IsValid) return false;

            if (!_channelByRuntimeId.TryGetValue(runtimeId.Value, out var channelType))
            {
                return false;
            }

            if (!_channels.TryGetValue(channelType, out var channel))
            {
                return false;
            }

            return channel.IsPlayingByRuntimeId(runtimeId);
        }

        /// <summary>
        /// runtime_id로 재생 정보를 조회한다.
        /// </summary>
        public bool TryGetPlayingInfo(SoundRuntimeId runtimeId, out PlayingInfo info)
        {
            info = default;

            if (!runtimeId.IsValid) return false;

            if (!_channelByRuntimeId.TryGetValue(runtimeId.Value, out var channelType))
            {
                return false;
            }

            if (!_channels.TryGetValue(channelType, out var channel))
            {
                return false;
            }

            return channel.TryGetPlayingInfo(runtimeId, out info);
        }

        // ====================================================================
        // Bulk Control API
        // ====================================================================

        /// <summary>
        /// 특정 sound_id의 모든 재생을 정지한다.
        /// </summary>
        public int StopAllBySoundId(string soundId)
        {
            int count = 0;
            foreach (var channel in _channels.Values)
            {
                count += channel.StopAllBySoundId(soundId);
            }
            return count;
        }

        /// <summary>
        /// 특정 채널의 모든 사운드를 정지한다.
        /// </summary>
        public int StopAllByChannel(SoundChannelType channelType)
        {
            if (!_channels.TryGetValue(channelType, out var channel))
            {
                return 0;
            }

            int count = channel.ActiveCount;
            channel.StopAll();
            return count;
        }

        /// <summary>
        /// 특정 채널의 모든 사운드를 정지한다.
        /// </summary>
        public void StopChannel(SoundChannelType channelType)
        {
            if (_channels.TryGetValue(channelType, out var channel))
            {
                channel.StopAll();
            }
        }

        /// <summary>
        /// 특정 그룹의 사운드를 정지한다.
        /// </summary>
        public void StopByGroup(int groupId)
        {
            foreach (var channel in _channels.Values)
            {
                channel.StopByGroup(groupId);
            }
        }

        /// <summary>
        /// 모든 사운드를 정지한다.
        /// </summary>
        public void StopAll()
        {
            foreach (var channel in _channels.Values)
            {
                channel.StopAll();
            }
        }

        // ====================================================================
        // Volume Control
        // ====================================================================

        /// <summary>
        /// 특정 채널의 볼륨을 설정한다.
        /// </summary>
        public void SetChannelVolume(SoundChannelType channelType, float volume)
        {
            if (_channels.TryGetValue(channelType, out var channel))
            {
                channel.SetVolume(volume);
            }
        }

        /// <summary>
        /// 특정 채널의 음소거를 설정한다.
        /// </summary>
        public void SetChannelMute(SoundChannelType channelType, bool mute)
        {
            if (_channels.TryGetValue(channelType, out var channel))
            {
                channel.SetMute(mute);
            }
        }

        // ====================================================================
        // Helpers
        // ====================================================================

        private static ISoundRow? _selectByWeight(List<ISoundRow> candidates)
        {
            if (candidates.Count == 0) return null;
            if (candidates.Count == 1) return candidates[0];

            int totalWeight = 0;
            foreach (var row in candidates)
            {
                var w = row.weight <= 0 ? 1 : row.weight;
                totalWeight += w;
            }

            var randomValue = UnityEngine.Random.Range(0, totalWeight);
            int cumulative = 0;

            foreach (var row in candidates)
            {
                var w = row.weight <= 0 ? 1 : row.weight;
                cumulative += w;
                if (randomValue < cumulative)
                {
                    return row;
                }
            }

            return candidates[candidates.Count - 1];
        }

        private static string _extractAssetName(string path)
        {
            if (string.IsNullOrEmpty(path)) return path;

            var lastSlash = path.LastIndexOfAny(new[] { '/', '\\' });
            var name = lastSlash >= 0 ? path.Substring(lastSlash + 1) : path;

            var dot = name.LastIndexOf('.');
            return dot >= 0 ? name.Substring(0, dot) : name;
        }

        private static bool _tryParseChannel(string channelName, out SoundChannelType channelType)
        {
            channelType = SoundChannelType.Effect;
            if (string.IsNullOrEmpty(channelName)) return false;

            switch (channelName.ToLowerInvariant())
            {
                case "bgm":
                    channelType = SoundChannelType.Bgm;
                    return true;
                case "effect":
                case "sfx":
                    channelType = SoundChannelType.Effect;
                    return true;
                case "ui":
                    channelType = SoundChannelType.Ui;
                    return true;
                case "voice":
                    channelType = SoundChannelType.Voice;
                    return true;
                default:
                    return false;
            }
        }

        // ====================================================================
        // Lifecycle
        // ====================================================================

        protected override void OnDestroy()
        {
            StopAll();
            _clipCacheByRowId.Clear();
            _loadedRowIdsByGameKey.Clear();
            _loadedBundleKeys.Clear();
            _loadedGameKeys.Clear();
            _channelByRuntimeId.Clear();
            _channels.Clear();
            base.OnDestroy();
        }

    }
}
