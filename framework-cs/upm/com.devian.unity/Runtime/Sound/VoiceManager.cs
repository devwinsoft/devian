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

        // ====================================================================
        // Resolve Cache
        // ====================================================================

        private readonly Dictionary<string, string> _voiceSoundIdByVoiceId = new();
        private readonly Dictionary<string, string> _subtitleKeyByVoiceId = new();
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
        // Resolve API
        // ====================================================================

        /// <summary>
        /// 지정된 언어로 TB_VOICE 전체를 Resolve하여 캐시를 구성한다.
        /// SystemLanguage는 이 메서드에서만 사용한다.
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
            _subtitleKeyByVoiceId.Clear();
            _currentLanguage = language;

            var allRows = GetAllVoiceRows();
            if (allRows == null) return;

            foreach (var row in allRows)
            {
                if (row == null) continue;

                // 1. 해당 언어의 sound_id 가져오기
                var soundId = row.GetSoundIdForLanguage(language);

                // 2. 비어있으면 fallback 적용
                if (string.IsNullOrEmpty(soundId))
                {
                    soundId = row.GetSoundIdForLanguage(_fallbackLanguage);
                }

                // 3. 여전히 비어있으면 경고 후 스킵
                if (string.IsNullOrEmpty(soundId))
                {
                    Log.Warn($"[VoiceManager] Voice '{row.voice_id}' has no sound_id for {language} or fallback {_fallbackLanguage}.");
                    continue;
                }

                // 4. 캐시 등록
                _voiceSoundIdByVoiceId[row.voice_id] = soundId;
                _subtitleKeyByVoiceId[row.voice_id] = row.text_l10n_key;
            }

            Log.Info($"[VoiceManager] Resolved {_voiceSoundIdByVoiceId.Count} voices for {language}.");
        }

        /// <summary>
        /// Resolve 캐시를 초기화한다.
        /// </summary>
        public void ClearResolveCache()
        {
            _voiceSoundIdByVoiceId.Clear();
            _subtitleKeyByVoiceId.Clear();
            _currentLanguage = SystemLanguage.Unknown;
        }

        // ====================================================================
        // Load Voice Clips
        // ====================================================================

        /// <summary>
        /// 현재 Resolve된 언어에 대한 Voice 클립들을 로드한다.
        /// 반드시 ResolveForLanguage() 호출 후에 사용한다.
        /// </summary>
        public IEnumerator LoadVoiceClipsAsync(string bundleKey, Action<string>? onError = null)
        {
            if (_currentLanguage == SystemLanguage.Unknown)
            {
                onError?.Invoke("[VoiceManager] Language not resolved. Call ResolveForLanguage() first.");
                yield break;
            }

            // AssetManager.LoadBundleAssets를 사용하여 언어별 클립 로드
            // 언어 intersection은 AssetManager가 처리
            yield return AssetManager.LoadBundleAssets<AudioClip>(bundleKey, _currentLanguage);
        }

        // ====================================================================
        // Playback API (캐시 조회만, SystemLanguage 분기 금지)
        // ====================================================================

        /// <summary>
        /// voice_id로 보이스를 재생한다.
        /// 재생 시점에는 캐시 조회만 수행한다 (SystemLanguage 파라미터 없음).
        /// </summary>
        public SoundPlay? PlayVoice(string voiceId, float volume = 1f, int groupId = 0)
        {
            // 1. 캐시에서 sound_id 조회
            if (!_voiceSoundIdByVoiceId.TryGetValue(voiceId, out var soundId))
            {
                Log.Warn($"[VoiceManager] Voice not resolved: {voiceId}");
                return null;
            }

            // 2. SoundManager로 재생 위임 (채널은 Voice로 고정)
            return SoundManager.Instance.Play(soundId, volume, groupId, channelOverride: "Voice");
        }

        /// <summary>
        /// voice_id에 해당하는 자막 키를 반환한다.
        /// </summary>
        public string? GetSubtitleKey(string voiceId)
        {
            if (_subtitleKeyByVoiceId.TryGetValue(voiceId, out var key))
            {
                return key;
            }
            return null;
        }

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

        // ====================================================================
        // Lifecycle
        // ====================================================================

        protected override void OnDestroy()
        {
            ClearResolveCache();
            base.OnDestroy();
        }
    }
}
