// SSOT: skills/devian-unity/22-sound-system/18-voice-table-resolve/SKILL.md

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
    /// - Voice clip 로딩은 key_bundle + language 기반으로 수행 (key_group 제거됨)
    /// - VOICE는 SOUND 테이블을 참조하지 않고 독립적으로 로드/재생한다
    /// - clip 경로는 IVoiceRow.TryGetClipColumn()으로 직접 조회
    ///
    /// AutoSingleton-based: 없으면 자동 생성. 씬에 CompoSingleton으로 배치하면 우선.
    /// </summary>
    public sealed class VoiceManager : AutoSingleton<VoiceManager>
    {

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
        /// key_bundle로 voice rows를 조회하는 델리게이트.
        /// LoadByBundleKeyAsync에서 사용한다.
        /// key_group은 제거됨 - 로드/언로드는 key_bundle 단위로만 수행.
        /// </summary>
        public Func<string, IEnumerable<IVoiceRow>>? GetVoiceRowsByBundleKey { get; set; }

        // ====================================================================
        // Resolve Cache
        // ====================================================================

        // voice_id → IVoiceRow 캐시 (Resolve 결과)
        private readonly Dictionary<string, IVoiceRow> _resolvedVoiceRows = new Dictionary<string, IVoiceRow>();
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
        // Loaded Voice Bundles (key_bundle 기반)
        // ====================================================================

        private readonly HashSet<string> _loadedVoiceBundleKeys = new HashSet<string>();

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
            _resolvedVoiceRows.Clear();
            _currentLanguage = language;

            // 컬럼명 구성: "clip_" + language.ToString()
            var col = "clip_" + language.ToString();
            var fallbackCol = "clip_" + _fallbackLanguage.ToString();

            var allRows = GetAllVoiceRows();
            if (allRows == null) return;

            foreach (var row in allRows)
            {
                if (row == null) continue;

                // 1. 해당 언어의 컬럼에서 clip 경로 가져오기
                if (!row.TryGetClipColumn(col, out var clipPath))
                {
                    // 2. 비어있으면 fallback 컬럼 시도
                    if (!row.TryGetClipColumn(fallbackCol, out clipPath))
                    {
                        // 3. 여전히 비어있으면 경고 후 스킵
                        Log.Warn($"[VoiceManager] Voice '{row.voice_id}' has no clip for {col} or fallback {fallbackCol}.");
                        continue;
                    }
                }

                // 4. 캐시 등록 (voice_id → IVoiceRow)
                _resolvedVoiceRows[row.voice_id] = row;
            }

            Log.Info($"[VoiceManager] Resolved {_resolvedVoiceRows.Count} voices for {language}.");
        }

        /// <summary>
        /// Resolve 캐시를 초기화한다.
        /// </summary>
        public void ClearResolveCache()
        {
            _resolvedVoiceRows.Clear();
            _currentLanguage = SystemLanguage.Unknown;
        }

        // ====================================================================
        // Voice Loading API (key_bundle + language 기반, SOUND 미참조)
        // key_group은 제거됨 - 로드/언로드는 key_bundle 단위로만 수행.
        // ====================================================================

        /// <summary>
        /// key_bundle 기반으로 Voice clip을 로드한다.
        /// 반드시 ResolveForLanguage() 호출 후에 사용한다.
        /// VOICE 테이블의 key_bundle/clip 경로를 직접 사용한다 (SOUND 테이블 미참조).
        /// </summary>
        /// <param name="bundleKey">TB_VOICE.key_bundle</param>
        /// <param name="language">Voice 언어</param>
        /// <param name="fallbackLanguage">Fallback 언어</param>
        /// <param name="onError">에러 콜백</param>
        public IEnumerator LoadByBundleKeyAsync(
            string bundleKey,
            SystemLanguage language,
            SystemLanguage fallbackLanguage,
            Action<string>? onError = null)
        {
            if (_loadedVoiceBundleKeys.Contains(bundleKey))
            {
                yield break;
            }

            if (_currentLanguage == SystemLanguage.Unknown)
            {
                onError?.Invoke("[VoiceManager] Language not resolved. Call ResolveForLanguage() first.");
                yield break;
            }

            // key_bundle에 해당하는 voice rows 수집
            IEnumerable<IVoiceRow>? voiceRows = null;

            if (GetVoiceRowsByBundleKey != null)
            {
                voiceRows = GetVoiceRowsByBundleKey(bundleKey);
            }
            else if (GetAllVoiceRows != null)
            {
                // Fallback: 전체 순회하면서 key_bundle 필터링
                var filteredRows = new List<IVoiceRow>();
                foreach (var row in GetAllVoiceRows())
                {
                    if (row != null && row.key_bundle == bundleKey)
                    {
                        filteredRows.Add(row);
                    }
                }
                voiceRows = filteredRows;
            }

            if (voiceRows == null)
            {
                onError?.Invoke($"[VoiceManager] No voice rows found for key_bundle: {bundleKey}");
                yield break;
            }

            // Resolve된 voice_id만 필터링
            var resolvedRows = new List<IVoiceRow>();

            foreach (var row in voiceRows)
            {
                if (row == null) continue;

                // Resolve 캐시에 존재하는지 확인
                if (_resolvedVoiceRows.ContainsKey(row.voice_id))
                {
                    resolvedRows.Add(row);
                }
            }

            if (resolvedRows.Count == 0)
            {
                Log.Warn($"[VoiceManager] No resolved voices for key_bundle: {bundleKey}");
                _loadedVoiceBundleKeys.Add(bundleKey);
                yield break;
            }

            // SoundManager 내부 헬퍼 호출 (VOICE row 직접 전달, SOUND 미참조)
            yield return SoundManager.Instance._loadVoiceClipsAsync(
                bundleKey,
                resolvedRows,
                language,
                fallbackLanguage,
                onError
            );

            _loadedVoiceBundleKeys.Add(bundleKey);
        }

        /// <summary>
        /// 여러 key_bundle을 순차적으로 로드한다.
        /// </summary>
        public IEnumerator LoadByBundleKeysAsync(
            IEnumerable<string> bundleKeys,
            SystemLanguage language,
            SystemLanguage fallbackLanguage,
            Action<string>? onError = null)
        {
            foreach (var bundleKey in bundleKeys)
            {
                yield return LoadByBundleKeyAsync(bundleKey, language, fallbackLanguage, onError);
            }
        }

        /// <summary>
        /// key_bundle 기반으로 Voice clip을 언로드한다.
        /// </summary>
        public void UnloadByBundleKey(string bundleKey)
        {
            if (!_loadedVoiceBundleKeys.Contains(bundleKey)) return;

            SoundManager.Instance.UnloadByBundleKey(bundleKey);

            _loadedVoiceBundleKeys.Remove(bundleKey);
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

        /// <summary>
        /// 모든 로드된 Voice bundle을 언로드한다.
        /// </summary>
        public void UnloadAllVoiceBundles()
        {
            foreach (var bundleKey in _loadedVoiceBundleKeys)
            {
                SoundManager.Instance.UnloadByBundleKey(bundleKey);
            }
            _loadedVoiceBundleKeys.Clear();
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
        /// <param name="pitch">피치 (0이면 row 기반)</param>
        /// <param name="groupId">그룹 ID</param>
        /// <returns>runtime_id (재생 실패 시 Invalid)</returns>
        public SoundRuntimeId PlayVoice(string voiceId, float volume = 1f, float pitch = 0f, int groupId = 0)
        {
            // 1. 캐시에서 IVoiceRow 조회
            if (!_resolvedVoiceRows.TryGetValue(voiceId, out var voiceRow))
            {
                Log.Warn($"[VoiceManager] Voice not resolved: {voiceId}");
                return SoundRuntimeId.Invalid;
            }

            // 2. SoundManager 내부 헬퍼로 재생 위임 (IVoiceRow 기반)
            return SoundManager.Instance._playVoiceInternal(voiceRow, volume, pitch, groupId, null);
        }

        /// <summary>
        /// voice_id로 3D 보이스를 재생한다.
        /// 재생 시점에는 캐시 조회만 수행한다 (SystemLanguage 파라미터 없음).
        /// 3D 파라미터(distance_near, distance_far)는 IVoiceRow에서 가져온다.
        /// </summary>
        /// <param name="voiceId">voice_id (TB_VOICE)</param>
        /// <param name="position">3D 위치</param>
        /// <param name="volume">볼륨 (0~1)</param>
        /// <param name="pitch">피치 (0이면 row 기반)</param>
        /// <param name="groupId">그룹 ID</param>
        /// <returns>runtime_id (재생 실패 시 Invalid)</returns>
        public SoundRuntimeId PlayVoice3D(string voiceId, Vector3 position, float volume = 1f, float pitch = 0f, int groupId = 0)
        {
            // 1. 캐시에서 IVoiceRow 조회
            if (!_resolvedVoiceRows.TryGetValue(voiceId, out var voiceRow))
            {
                Log.Warn($"[VoiceManager] Voice not resolved: {voiceId}");
                return SoundRuntimeId.Invalid;
            }

            // 2. SoundManager 내부 헬퍼로 3D 재생 위임 (IVoiceRow 기반)
            return SoundManager.Instance._playVoiceInternal(voiceRow, volume, pitch, groupId, position);
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
        /// voice_id 자체를 자막 키로 사용.
        /// </summary>
        public string? GetCaptionKey(string voiceId)
        {
            // voice_id 자체가 자막 키
            if (_resolvedVoiceRows.ContainsKey(voiceId))
            {
                return voiceId;
            }
            return null;
        }

        /// <summary>
        /// [Deprecated] GetSubtitleKey → GetCaptionKey로 변경됨.
        /// </summary>
        [Obsolete("Use GetCaptionKey instead.")]
        public string? GetSubtitleKey(string voiceId) => GetCaptionKey(voiceId);

        /// <summary>
        /// voice_id가 Resolve 캐시에 존재하는지 확인한다.
        /// </summary>
        public bool IsVoiceResolved(string voiceId)
        {
            return _resolvedVoiceRows.ContainsKey(voiceId);
        }

        /// <summary>
        /// Resolve된 voice_id에 해당하는 IVoiceRow를 반환한다.
        /// </summary>
        public IVoiceRow? GetResolvedVoiceRow(string voiceId)
        {
            if (_resolvedVoiceRows.TryGetValue(voiceId, out var row))
            {
                return row;
            }
            return null;
        }

        /// <summary>
        /// 특정 key_bundle이 로드되어 있는지 확인한다.
        /// </summary>
        public bool IsBundleKeyLoaded(string bundleKey)
        {
            return _loadedVoiceBundleKeys.Contains(bundleKey);
        }

        // ====================================================================
        // Lifecycle
        // ====================================================================

        protected override void OnDestroy()
        {
            UnloadAllVoiceBundles();
            ClearResolveCache();
            base.OnDestroy();
        }
    }
}
