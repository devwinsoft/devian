// SSOT: skills/devian-unity/30-unity-components/18-voice-table-resolve/SKILL.md

#nullable enable

using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Devian
{
    /// <summary>
    /// Voice 재생 관리자.
    /// - TB_VOICE를 로딩 시점에 "현재 언어용 맵"으로 Resolve 캐시
    /// - 재생 시점에는 캐시 조회만 수행 (SystemLanguage 분기 금지)
    /// - Voice clip 로딩은 group_key + language 기반으로 수행
    /// </summary>
    public sealed class VoiceManager : AutoSingleton<VoiceManager>
    {
        // Voice group key prefix
        private const string VOICE_GROUP_PREFIX = "VOICE::";

        // ====================================================================
        // Voice Table Registry
        // ====================================================================

        /// <summary>
        /// voice_id로 row를 조회하는 델리게이트.
        /// 프로젝트에서 TB_VOICE 컨테이너를 연결한다.
        /// </summary>
        public Func<string, IVoiceRow?>? GetVoiceRow { get; set; }

        /// <summary>
        /// 모든 voice row를 순회하는 델리게이트.
        /// Resolve 시점에 전체 테이블을 캐시로 구성할 때 사용한다.
        /// </summary>
        public Func<IEnumerable<IVoiceRow>>? GetAllVoiceRows { get; set; }

        /// <summary>
        /// group_key로 voice rows를 조회하는 델리게이트.
        /// LoadByGroupKeyAsync에서 사용한다.
        /// </summary>
        public Func<string, IEnumerable<IVoiceRow>>? GetVoiceRowsByGroupKey { get; set; }

        // ====================================================================
        // Resolve Cache
        // ====================================================================

        private readonly Dictionary<string, string> _voiceSoundIdByVoiceId = new();
        // text_l10n_key 제거됨 - 자막 키가 필요하면 voice_id 자체를 사용
        private SystemLanguage _currentLanguage = SystemLanguage.Unknown;
        private SystemLanguage _fallbackLanguage = SystemLanguage.English;

        /// <summary>
        /// 현재 Resolve된 언어.
        /// </summary>
        public SystemLanguage CurrentLanguage => _currentLanguage;

        /// <summary>
        /// Fallback 언어 (기본: English).
        /// </summary>
        public SystemLanguage FallbackLanguage
        {
            get => _fallbackLanguage;
            set => _fallbackLanguage = value;
        }

        // ====================================================================
        // Loaded Voice Groups
        // ====================================================================

        private readonly HashSet<string> _loadedVoiceGroupKeys = new();

        // ====================================================================
        // Resolve API
        // ====================================================================

        /// <summary>
        /// 지정된 언어로 TB_VOICE 전체를 Resolve하여 캐시를 구성한다.
        /// SystemLanguage는 이 메서드에서만 사용한다 (재생 시점에는 절대 사용 금지).
        /// </summary>
        public void ResolveForLanguage(SystemLanguage language)
        {
            if (GetAllVoiceRows == null)
            {
                Log.Warn("[VoiceManager] GetAllVoiceRows delegate not set.");
                return;
            }

            // 캐시 초기화
            _voiceSoundIdByVoiceId.Clear();
            _currentLanguage = language;

            // 컬럼명 구성: "clip_" + language.ToString()
            var col = "clip_" + language.ToString();
            var fallbackCol = "clip_" + _fallbackLanguage.ToString();

            var allRows = GetAllVoiceRows();
            if (allRows == null) return;

            foreach (var row in allRows)
            {
                if (row == null) continue;

                // 1. 해당 언어의 컬럼에서 sound_id 가져오기
                if (!row.TryGetClipColumn(col, out var soundId))
                {
                    // 2. 비어있으면 fallback 컬럼 시도
                    if (!row.TryGetClipColumn(fallbackCol, out soundId))
                    {
                        // 3. 여전히 비어있으면 경고 후 스킵
                        Log.Warn($"[VoiceManager] Voice '{row.voice_id}' has no sound_id for {col} or fallback {fallbackCol}.");
                        continue;
                    }
                }

                // 4. 캐시 등록
                _voiceSoundIdByVoiceId[row.voice_id] = soundId;
                // text_l10n_key 제거됨 - 자막 키가 필요하면 voice_id 자체를 사용
            }

            Log.Info($"[VoiceManager] Resolved {_voiceSoundIdByVoiceId.Count} voices for {language}.");
        }

        /// <summary>
        /// Resolve 캐시를 초기화한다.
        /// </summary>
        public void ClearResolveCache()
        {
            _voiceSoundIdByVoiceId.Clear();
            _currentLanguage = SystemLanguage.Unknown;
        }

        // ====================================================================
        // Voice Loading API (group_key + language 기반)
        // ====================================================================

        /// <summary>
        /// group_key 기반으로 Voice clip을 로드한다.
        /// 반드시 ResolveForLanguage() 호출 후에 사용한다.
        /// Resolve 결과로 나온 sound_id들만 로드한다.
        /// </summary>
        /// <param name="groupKey">TB_VOICE.group_key</param>
        /// <param name="language">Voice 언어</param>
        /// <param name="fallbackLanguage">Fallback 언어</param>
        /// <param name="onError">에러 콜백</param>
        public IEnumerator LoadByGroupKeyAsync(
            string groupKey,
            SystemLanguage language,
            SystemLanguage fallbackLanguage,
            Action<string>? onError = null)
        {
            var voiceGroupKey = VOICE_GROUP_PREFIX + groupKey;

            if (_loadedVoiceGroupKeys.Contains(groupKey))
            {
                yield break;
            }

            if (_currentLanguage == SystemLanguage.Unknown)
            {
                onError?.Invoke("[VoiceManager] Language not resolved. Call ResolveForLanguage() first.");
                yield break;
            }

            // group_key에 해당하는 voice rows 수집
            IEnumerable<IVoiceRow>? voiceRows = null;

            if (GetVoiceRowsByGroupKey != null)
            {
                voiceRows = GetVoiceRowsByGroupKey(groupKey);
            }
            else if (GetAllVoiceRows != null)
            {
                // Fallback: 전체 순회하면서 group_key 필터링
                var filteredRows = new List<IVoiceRow>();
                foreach (var row in GetAllVoiceRows())
                {
                    if (row != null && row.group_key == groupKey)
                    {
                        filteredRows.Add(row);
                    }
                }
                voiceRows = filteredRows;
            }

            if (voiceRows == null)
            {
                onError?.Invoke($"[VoiceManager] No voice rows found for group_key: {groupKey}");
                yield break;
            }

            // Resolve된 sound_id 집합 수집 (중복 제거)
            var soundIds = new HashSet<string>();

            foreach (var row in voiceRows)
            {
                if (row == null) continue;

                // Resolve 캐시에서 sound_id 조회
                if (_voiceSoundIdByVoiceId.TryGetValue(row.voice_id, out var soundId))
                {
                    if (!string.IsNullOrEmpty(soundId))
                    {
                        soundIds.Add(soundId);
                    }
                }
            }

            if (soundIds.Count == 0)
            {
                Log.Warn($"[VoiceManager] No resolved sound_ids for group_key: {groupKey}");
                _loadedVoiceGroupKeys.Add(groupKey);
                yield break;
            }

            // SoundManager 내부 헬퍼 호출
            yield return SoundManager.Instance._loadVoiceBySoundIdsAsync(
                voiceGroupKey,
                soundIds,
                language,
                fallbackLanguage,
                onError
            );

            _loadedVoiceGroupKeys.Add(groupKey);
        }

        /// <summary>
        /// group_key 기반으로 Voice clip을 언로드한다.
        /// </summary>
        public void UnloadByGroupKey(string groupKey)
        {
            if (!_loadedVoiceGroupKeys.Contains(groupKey)) return;

            var voiceGroupKey = VOICE_GROUP_PREFIX + groupKey;
            SoundManager.Instance.UnloadByKey(voiceGroupKey);

            _loadedVoiceGroupKeys.Remove(groupKey);
        }

        /// <summary>
        /// 모든 로드된 Voice group을 언로드한다.
        /// </summary>
        public void UnloadAllVoiceGroups()
        {
            foreach (var groupKey in _loadedVoiceGroupKeys)
            {
                var voiceGroupKey = VOICE_GROUP_PREFIX + groupKey;
                SoundManager.Instance.UnloadByKey(voiceGroupKey);
            }
            _loadedVoiceGroupKeys.Clear();
        }

        // ====================================================================
        // Playback API (캐시 조회만, SystemLanguage 분기 금지)
        // ====================================================================

        /// <summary>
        /// voice_id로 2D 보이스를 재생한다.
        /// 재생 시점에는 캐시 조회만 수행한다 (SystemLanguage 파라미터 없음).
        /// </summary>
        /// <param name="voiceId">voice_id (TB_VOICE)</param>
        /// <param name="volume">볼륨 (0~1)</param>
        /// <param name="pitch">피치</param>
        /// <param name="groupId">그룹 ID</param>
        /// <returns>runtime_id (재생 실패 시 Invalid)</returns>
        public SoundRuntimeId PlayVoice(string voiceId, float volume = 1f, float pitch = 1f, int groupId = 0)
        {
            // 1. 캐시에서 sound_id 조회
            if (!_voiceSoundIdByVoiceId.TryGetValue(voiceId, out var soundId))
            {
                Log.Warn($"[VoiceManager] Voice not resolved: {voiceId}");
                return SoundRuntimeId.Invalid;
            }

            // 2. SoundManager로 재생 위임 (채널은 Voice로 고정)
            return SoundManager.Instance.PlaySound(soundId, volume, pitch, groupId, channelOverride: "Voice");
        }

        /// <summary>
        /// voice_id로 3D 보이스를 재생한다.
        /// 재생 시점에는 캐시 조회만 수행한다 (SystemLanguage 파라미터 없음).
        /// 3D 파라미터(distance_near, distance_far)는 Resolve된 sound_id의 SOUND row에서 가져온다.
        /// </summary>
        /// <param name="voiceId">voice_id (TB_VOICE)</param>
        /// <param name="position">3D 위치</param>
        /// <param name="volume">볼륨 (0~1)</param>
        /// <param name="pitch">피치</param>
        /// <param name="groupId">그룹 ID</param>
        /// <returns>runtime_id (재생 실패 시 Invalid)</returns>
        public SoundRuntimeId PlayVoice3D(string voiceId, Vector3 position, float volume = 1f, float pitch = 1f, int groupId = 0)
        {
            // 1. 캐시에서 sound_id 조회
            if (!_voiceSoundIdByVoiceId.TryGetValue(voiceId, out var soundId))
            {
                Log.Warn($"[VoiceManager] Voice not resolved: {voiceId}");
                return SoundRuntimeId.Invalid;
            }

            // 2. SoundManager로 3D 재생 위임 (채널은 Voice로 고정)
            return SoundManager.Instance.PlaySound3D(soundId, position, volume, pitch, groupId, channelOverride: "Voice");
        }

        /// <summary>
        /// runtime_id로 보이스 재생을 정지한다.
        /// </summary>
        public bool StopVoice(SoundRuntimeId runtimeId)
        {
            return SoundManager.Instance.StopSound(runtimeId);
        }

        /// <summary>
        /// runtime_id로 보이스 재생을 일시정지한다.
        /// </summary>
        public bool PauseVoice(SoundRuntimeId runtimeId)
        {
            return SoundManager.Instance.PauseSound(runtimeId);
        }

        /// <summary>
        /// runtime_id로 보이스 재생을 재개한다.
        /// </summary>
        public bool ResumeVoice(SoundRuntimeId runtimeId)
        {
            return SoundManager.Instance.ResumeSound(runtimeId);
        }

        /// <summary>
        /// runtime_id로 보이스가 재생 중인지 확인한다.
        /// </summary>
        public bool IsVoicePlaying(SoundRuntimeId runtimeId)
        {
            return SoundManager.Instance.IsPlaying(runtimeId);
        }

        /// <summary>
        /// voice_id에 해당하는 자막 키를 반환한다.
        /// text_l10n_key 제거됨 - voice_id 자체를 자막 키로 사용.
        /// </summary>
        public string? GetCaptionKey(string voiceId)
        {
            // voice_id 자체가 자막 키
            if (_voiceSoundIdByVoiceId.ContainsKey(voiceId))
            {
                return voiceId;
            }
            return null;
        }

        /// <summary>
        /// [Deprecated] GetSubtitleKey → GetCaptionKey로 변경됨.
        /// </summary>
        [Obsolete("Use GetCaptionKey instead. text_l10n_key has been removed.")]
        public string? GetSubtitleKey(string voiceId) => GetCaptionKey(voiceId);

        /// <summary>
        /// voice_id가 Resolve 캐시에 존재하는지 확인한다.
        /// </summary>
        public bool IsVoiceResolved(string voiceId)
        {
            return _voiceSoundIdByVoiceId.ContainsKey(voiceId);
        }

        /// <summary>
        /// Resolve된 voice_id에 매핑된 sound_id를 반환한다.
        /// </summary>
        public string? GetResolvedSoundId(string voiceId)
        {
            if (_voiceSoundIdByVoiceId.TryGetValue(voiceId, out var soundId))
            {
                return soundId;
            }
            return null;
        }

        /// <summary>
        /// 특정 group_key가 로드되어 있는지 확인한다.
        /// </summary>
        public bool IsGroupKeyLoaded(string groupKey)
        {
            return _loadedVoiceGroupKeys.Contains(groupKey);
        }

        // ====================================================================
        // Lifecycle
        // ====================================================================

        protected override void OnDestroy()
        {
            UnloadAllVoiceGroups();
            ClearResolveCache();
            base.OnDestroy();
        }
    }
}
