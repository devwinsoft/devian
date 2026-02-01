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
    /// - PlaySound는 SoundRuntimeId를 반환하고, 모든 제어는 runtime_id 기반
    /// - isBundle=false일 경우 Resources.Load를 사용한다
    /// - channel은 SoundChannelType enum으로 직접 비교한다
    /// - Voice 채널 로딩 지원 (VoiceManager가 호출)
    ///
    /// AutoSingleton-based: 없으면 자동 생성. 씬에 CompoSingleton으로 배치하면 우선.
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
        /// key_bundle(로드/언로드 단위 키)로 rows를 조회하는 델리게이트 (Load/Unload용).
        /// key_group은 제거됨 - 로드/언로드는 key_bundle 단위로만 수행.
        /// </summary>
        public Func<string, IEnumerable<ISoundRow>>? GetSoundRowsByBundleKey { get; set; }

        // ====================================================================
        // Channels
        // ====================================================================

        private readonly Dictionary<SoundChannelType, SoundChannel> _channels = new Dictionary<SoundChannelType, SoundChannel>();

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
        private readonly Dictionary<int, SoundChannelType> _channelByRuntimeId = new Dictionary<int, SoundChannelType>();

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
        private readonly Dictionary<int, AudioClip> _clipCacheByRowId = new Dictionary<int, AudioClip>();

        // bundleKey → 로드된 row_id 목록 (언로드용)
        private readonly Dictionary<string, List<int>> _loadedRowIdsByBundleKey = new Dictionary<string, List<int>>();

        // 로드된 bundle_key 세트
        private readonly HashSet<string> _loadedBundleKeys = new HashSet<string>();

        // bundleKey → isBundle (언로드 시 분기용)
        private readonly Dictionary<string, bool> _bundleKeyIsBundle = new Dictionary<string, bool>();

        // ====================================================================
        // Loading Policy (key_bundle 단위, isBundle=true→Bundle, isBundle=false→Resource API)
        // key_group은 제거됨 - 로드/언로드는 key_bundle 단위로만 수행.
        // ====================================================================

        /// <summary>
        /// key_bundle 기준으로 사운드를 로드한다.
        /// Voice 채널은 로드하지 않는다 (VoiceManager.LoadByBundleKeyAsync 사용).
        /// </summary>
        public IEnumerator LoadByBundleKeyAsync(string bundleKey, Action<string>? onError = null)
        {
            if (_loadedBundleKeys.Contains(bundleKey))
            {
                yield break;
            }

            if (GetSoundRowsByBundleKey == null)
            {
                onError?.Invoke("[SoundManager] GetSoundRowsByBundleKey delegate not set.");
                yield break;
            }

            var rows = GetSoundRowsByBundleKey(bundleKey);
            if (rows == null)
            {
                _loadedBundleKeys.Add(bundleKey);
                yield break;
            }

            // isBundle 분기를 위해 첫 row 확인
            var rowList = new List<ISoundRow>();
            bool? isBundle = null;

            foreach (var row in rows)
            {
                if (row == null) continue;

                // Voice 채널 제외 (Voice는 VoiceManager가 담당)
                if (row.channel == SoundChannelType.Voice)
                {
                    continue;
                }

                rowList.Add(row);

                if (!isBundle.HasValue)
                {
                    isBundle = row.isBundle;
                }
            }

            if (rowList.Count == 0)
            {
                _loadedBundleKeys.Add(bundleKey);
                yield break;
            }

            var loadedRowIds = new List<int>();
            var actualIsBundle = isBundle ?? true;

            if (actualIsBundle)
            {
                // Bundle 로드: AssetManager.LoadBundleAssets
                yield return AssetManager.LoadBundleAssets<AudioClip>(bundleKey);

                foreach (var row in rowList)
                {
                    var assetName = AudioAssetNameUtil.ExtractAssetName(row.path);
                    var clip = AssetManager.GetAsset<AudioClip>(assetName);
                    if (clip != null)
                    {
                        _clipCacheByRowId[row.row_id] = clip;
                        loadedRowIds.Add(row.row_id);
                    }
                    else
                    {
                        onError?.Invoke($"[SoundManager] AudioClip not found for row_id '{row.row_id}', path '{row.path}'.");
                    }
                }
            }
            else
            {
                // Resource 로드: AssetManager.LoadResourceAssets (그룹 단위)
                yield return AssetManager.LoadResourceAssets<AudioClip>(bundleKey);

                foreach (var row in rowList)
                {
                    var assetName = AudioAssetNameUtil.ExtractAssetName(row.path);
                    var clip = AssetManager.GetAsset<AudioClip>(assetName);
                    if (clip != null)
                    {
                        _clipCacheByRowId[row.row_id] = clip;
                        loadedRowIds.Add(row.row_id);
                    }
                    else
                    {
                        onError?.Invoke($"[SoundManager] Resource AudioClip not found for row_id '{row.row_id}', path '{row.path}'.");
                    }
                }
            }

            // 언로드용 row_id 목록 저장
            _loadedRowIdsByBundleKey[bundleKey] = loadedRowIds;
            _bundleKeyIsBundle[bundleKey] = actualIsBundle;
            _loadedBundleKeys.Add(bundleKey);
        }

        /// <summary>
        /// 여러 key_bundle을 순차적으로 로드한다.
        /// </summary>
        public IEnumerator LoadByBundleKeysAsync(IEnumerable<string> bundleKeys, Action<string>? onError = null)
        {
            foreach (var bundleKey in bundleKeys)
            {
                yield return LoadByBundleKeyAsync(bundleKey, onError);
            }
        }

        /// <summary>
        /// key_bundle 기준으로 사운드를 언로드한다.
        /// row 캐시 제거 + AssetManager 언로드를 동기화한다.
        /// </summary>
        public void UnloadByBundleKey(string bundleKey)
        {
            if (!_loadedBundleKeys.Contains(bundleKey)) return;

            // 해당 bundleKey의 row_id들을 캐시에서 제거
            if (_loadedRowIdsByBundleKey.TryGetValue(bundleKey, out var rowIds))
            {
                foreach (var rowId in rowIds)
                {
                    _clipCacheByRowId.Remove(rowId);
                }
                _loadedRowIdsByBundleKey.Remove(bundleKey);
            }

            // AssetManager 언로드 (isBundle 분기)
            if (_bundleKeyIsBundle.TryGetValue(bundleKey, out var isBundle))
            {
                if (isBundle)
                {
                    AssetManager.UnloadBundleAssets(bundleKey);
                }
                else
                {
                    AssetManager.UnloadResourceAssets<AudioClip>(bundleKey);
                }
                _bundleKeyIsBundle.Remove(bundleKey);
            }

            _loadedBundleKeys.Remove(bundleKey);
        }

        /// <summary>
        /// 여러 key_bundle을 언로드한다.
        /// </summary>
        public void UnloadByBundleKeys(IEnumerable<string> bundleKeys)
        {
            foreach (var bundleKey in bundleKeys)
            {
                UnloadByBundleKey(bundleKey);
            }
        }

        // ====================================================================
        // Voice Loading Helper (VoiceManager 전용, internal)
        // ====================================================================

        /// <summary>
        /// VoiceManager가 호출하는 Voice clip 로드 헬퍼.
        /// IVoiceRow 집합을 받아 해당 voice clip을 로드한다.
        /// VOICE는 SOUND 테이블을 참조하지 않고 독립적으로 로드한다.
        /// </summary>
        internal IEnumerator _loadVoiceClipsAsync(
            string bundleKey,
            IEnumerable<IVoiceRow> voiceRows,
            SystemLanguage language,
            SystemLanguage fallbackLanguage,
            Action<string>? onError = null)
        {
            if (_loadedBundleKeys.Contains(bundleKey))
            {
                yield break;
            }

            // Voice clip 정보 수집
            var voiceClipInfos = new List<VoiceClipLoadInfo>();
            var loadedRowIds = new List<int>();

            // 언어 컬럼명 결정
            var langCol = "clip_" + language.ToString();
            var fallbackCol = "clip_" + fallbackLanguage.ToString();

            foreach (var row in voiceRows)
            {
                if (row == null) continue;

                // clip 경로 resolve
                string clipPath;
                if (!row.TryGetClipColumn(langCol, out clipPath))
                {
                    if (!row.TryGetClipColumn(fallbackCol, out clipPath))
                    {
                        // 경로 없음 - 스킵
                        continue;
                    }
                }

                if (string.IsNullOrEmpty(clipPath)) continue;

                // voice_id를 row_id 해시로 사용 (VOICE는 PK가 string)
                var rowIdHash = row.voice_id.GetHashCode();

                // 중복 체크
                bool exists = false;
                foreach (var info in voiceClipInfos)
                {
                    if (info.RowIdHash == rowIdHash)
                    {
                        exists = true;
                        break;
                    }
                }

                if (!exists)
                {
                    voiceClipInfos.Add(new VoiceClipLoadInfo(row, clipPath, rowIdHash));
                    loadedRowIds.Add(rowIdHash);
                }
            }

            // Voice 번들 로드 (VOICE는 isBundle=true 고정)
            if (language != SystemLanguage.Unknown)
            {
                yield return AssetManager.LoadBundleAssets<AudioClip>(bundleKey, language);
            }
            else
            {
                yield return AssetManager.LoadBundleAssets<AudioClip>(bundleKey);
            }

            // clip 캐시 등록
            _populateVoiceClipCache(voiceClipInfos, onError);

            // 언로드용 row_id 목록 저장
            _loadedRowIdsByBundleKey[bundleKey] = loadedRowIds;
            _bundleKeyIsBundle[bundleKey] = true; // VOICE는 항상 Bundle
            _loadedBundleKeys.Add(bundleKey);
        }

        /// <summary>
        /// Voice clip 로드 정보.
        /// </summary>
        private readonly struct VoiceClipLoadInfo
        {
            public readonly IVoiceRow Row;
            public readonly string ClipPath;
            public readonly int RowIdHash;

            public VoiceClipLoadInfo(IVoiceRow row, string clipPath, int rowIdHash)
            {
                Row = row;
                ClipPath = clipPath;
                RowIdHash = rowIdHash;
            }
        }

        private void _populateVoiceClipCache(List<VoiceClipLoadInfo> infos, Action<string>? onError)
        {
            int loadedCount = 0;
            foreach (var info in infos)
            {
                var assetName = AudioAssetNameUtil.ExtractAssetName(info.ClipPath);
                var clip = AssetManager.GetAsset<AudioClip>(assetName);
                if (clip != null)
                {
                    _clipCacheByRowId[info.RowIdHash] = clip;
                    loadedCount++;
                }
                else
                {
                    onError?.Invoke($"[SoundManager] Voice AudioClip not found for voice_id '{info.Row.voice_id}', path '{info.ClipPath}'.");
                }
            }

            if (infos.Count > 0 && loadedCount == 0)
            {
                onError?.Invoke($"[SoundManager] No Voice AudioClips loaded (expected {infos.Count} clips).");
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
            float pitch = 0f,
            int groupId = 0,
            SoundChannelType? channelOverride = null)
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
            float pitch = 0f,
            int groupId = 0,
            SoundChannelType? channelOverride = null)
        {
            return _playInternal(soundId, volume, pitch, groupId, position, channelOverride);
        }

        /// <summary>
        /// 내부 재생 구현 (2D/3D 공통).
        /// BaseAudioManager에 볼륨/피치 계산을 위임한다.
        /// </summary>
        private SoundRuntimeId _playInternal(
            string soundId,
            float volume,
            float pitchOverride,
            int groupId,
            Vector3? position,
            SoundChannelType? channelOverride)
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

            // 5. 채널 결정 (enum 직접 비교)
            var channelType = channelOverride ?? selectedRow.channel;

            if (!_channels.TryGetValue(channelType, out var channel))
            {
                Log.Warn($"[SoundManager] Channel not found: {channelType}");
                return SoundRuntimeId.Invalid;
            }

            // 6. runtime_id 발급
            var runtimeId = _allocateRuntimeId();

            // 7. BaseAudioManager.TryPlay()로 위임
            var success = BaseAudioManager.TryPlay(
                channel,
                runtimeId,
                soundId,  // 쿨타임 공유를 위해 sound_id 전달
                selectedRow.row_id,
                selectedRow,
                clip,
                volume,
                pitchOverride,
                groupId,
                position
            );

            if (!success)
            {
                // 쿨타임 등으로 재생 실패
                return SoundRuntimeId.Invalid;
            }

            // 8. channel 매핑 등록
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
            float pitch = 0f,
            int groupId = 0,
            SoundChannelType? channelOverride = null)
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
            float pitch = 0f,
            int groupId = 0,
            SoundChannelType? channelOverride = null)
        {
            runtimeId = PlaySound3D(soundId, position, volume, pitch, groupId, channelOverride);
            return runtimeId.IsValid;
        }

        // ====================================================================
        // Voice Play API (VoiceManager 전용, internal)
        // ====================================================================

        /// <summary>
        /// VoiceManager가 호출하는 Voice 재생 헬퍼.
        /// IVoiceRow 기반으로 재생한다.
        /// </summary>
        internal SoundRuntimeId _playVoiceInternal(
            IVoiceRow voiceRow,
            float volume,
            float pitchOverride,
            int groupId,
            Vector3? position)
        {
            // voice_id 해시로 clip 조회
            var rowIdHash = voiceRow.voice_id.GetHashCode();

            if (!_clipCacheByRowId.TryGetValue(rowIdHash, out var clip))
            {
                Log.Warn($"[SoundManager] Voice AudioClip not loaded: {voiceRow.voice_id}");
                return SoundRuntimeId.Invalid;
            }

            // Voice 채널 고정
            if (!_channels.TryGetValue(SoundChannelType.Voice, out var channel))
            {
                Log.Warn("[SoundManager] Voice channel not found.");
                return SoundRuntimeId.Invalid;
            }

            // runtime_id 발급
            var runtimeId = _allocateRuntimeId();

            // BaseAudioManager.TryPlay()로 위임
            var success = BaseAudioManager.TryPlay(
                channel,
                runtimeId,
                voiceRow.voice_id,  // logicalId
                rowIdHash,          // rowId
                voiceRow,
                clip,
                volume,
                pitchOverride,
                groupId,
                position
            );

            if (!success)
            {
                return SoundRuntimeId.Invalid;
            }

            _channelByRuntimeId[runtimeId.Value] = SoundChannelType.Voice;

            return runtimeId;
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

        // ====================================================================
        // Lifecycle
        // ====================================================================

        protected override void OnDestroy()
        {
            StopAll();
            _clipCacheByRowId.Clear();
            _loadedRowIdsByBundleKey.Clear();
            _loadedBundleKeys.Clear();
            _bundleKeyIsBundle.Clear();
            _channelByRuntimeId.Clear();
            _channels.Clear();
            base.OnDestroy();
        }

    }
}
