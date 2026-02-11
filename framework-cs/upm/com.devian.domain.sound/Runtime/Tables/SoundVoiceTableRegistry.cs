// SSOT: skills/devian-unity/22-sound-system/17-sound-manager/SKILL.md
// 수기 유지 파일 (Generated 아님)
// Sound/Voice 테이블과 Manager를 연결하는 레지스트리.
// Generated 클래스(SOUND, VOICE)를 Adapter로 ISoundRow/IVoiceRow 인터페이스에 맞춘다.
//
// NOTE: TbLoader 등록은 DomainTableRegistry (Generated)가 담당.
//       이 클래스는 Manager 델리게이트 연결 + 어댑터 캐시만 관리한다.
// v10 변경: key_group 제거, key_bundle 중심 로드/언로드

#nullable enable

using System.Collections.Generic;
using UnityEngine;

namespace Devian.Domain.Sound
{
    /// <summary>
    /// Sound/Voice 테이블과 Manager를 연결한다.
    /// - TB_SOUND → SoundManager.GetSoundRowsBySoundId / GetSoundRowsByBundleKey
    /// - TB_VOICE → VoiceManager.GetVoiceRow / GetAllVoiceRows / GetVoiceRowsByBundleKey
    /// - Generated 클래스를 Adapter로 ISoundRow/IVoiceRow 인터페이스에 맞춘다.
    /// key_group은 제거됨 - 로드/언로드는 key_bundle 단위로만 수행.
    /// </summary>
    internal static class SoundVoiceTableRegistry
    {
        // Adapter 캐시 (SOUND/VOICE → ISoundRow/IVoiceRow)
        private static readonly Dictionary<int, SoundRowAdapter> _soundAdapterCache = new();
        private static readonly Dictionary<string, VoiceRowAdapter> _voiceAdapterCache = new();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Register()
        {
            // SoundManager 연결 (v10: GetSoundRowsByBundleKey)
            SoundManager.Instance.GetSoundRowsBySoundId = GetSoundRowsBySoundId;
            SoundManager.Instance.GetSoundRowsByBundleKey = GetSoundRowsByBundleKey;

            // VoiceManager 연결 (v10: GetVoiceRowsByBundleKey)
            VoiceManager.Instance.GetVoiceRow = GetVoiceRow;
            VoiceManager.Instance.GetAllVoiceRows = GetAllVoiceRows;
            VoiceManager.Instance.GetVoiceRowsByBundleKey = GetVoiceRowsByBundleKey;

            // NOTE: TbLoader 등록은 DomainTableRegistry (Generated)가 담당.
            // TB_SOUND._AfterLoad() / TB_VOICE._AfterLoad()가 호출되어
            // _OnAfterLoad()에서 어댑터 캐시 클리어 + 인덱스 빌드가 자동 수행됨.
        }

        /// <summary>
        /// sound_id로 후보 rows를 조회하고 ISoundRow 어댑터로 래핑한다.
        /// </summary>
        private static IReadOnlyList<ISoundRow> GetSoundRowsBySoundId(string soundId)
        {
            var rows = TB_SOUND.GetRows(soundId);
            if (rows.Count == 0) return System.Array.Empty<ISoundRow>();

            var result = new List<ISoundRow>(rows.Count);
            foreach (var row in rows)
            {
                result.Add(GetOrCreateSoundAdapter(row));
            }
            return result;
        }

        /// <summary>
        /// key_bundle로 rows를 조회하고 ISoundRow 어댑터로 래핑한다.
        /// </summary>
        private static IEnumerable<ISoundRow> GetSoundRowsByBundleKey(string bundleKey)
        {
            var rows = TB_SOUND.GetRowsByBundleKey(bundleKey);
            if (rows.Count == 0) yield break;

            foreach (var row in rows)
            {
                yield return GetOrCreateSoundAdapter(row);
            }
        }

        /// <summary>
        /// voice_id로 row를 조회하고 IVoiceRow 어댑터로 래핑한다.
        /// </summary>
        private static IVoiceRow? GetVoiceRow(string voiceId)
        {
            var row = TB_VOICE.Get(voiceId);
            if (row == null) return null;

            return GetOrCreateVoiceAdapter(row);
        }

        /// <summary>
        /// 모든 voice rows를 IVoiceRow 어댑터로 래핑하여 반환한다.
        /// </summary>
        private static IEnumerable<IVoiceRow> GetAllVoiceRows()
        {
            foreach (var row in TB_VOICE.GetAll())
            {
                yield return GetOrCreateVoiceAdapter(row);
            }
        }

        /// <summary>
        /// key_bundle로 voice rows를 조회하고 IVoiceRow 어댑터로 래핑한다.
        /// </summary>
        private static IEnumerable<IVoiceRow> GetVoiceRowsByBundleKey(string bundleKey)
        {
            var rows = TB_VOICE.GetRowsByBundleKey(bundleKey);
            foreach (var row in rows)
            {
                yield return GetOrCreateVoiceAdapter(row);
            }
        }

        /// <summary>
        /// SOUND → SoundRowAdapter 캐시 조회/생성.
        /// </summary>
        private static SoundRowAdapter GetOrCreateSoundAdapter(SOUND row)
        {
            if (!_soundAdapterCache.TryGetValue(row.Row_id, out var adapter))
            {
                adapter = new SoundRowAdapter(row);
                _soundAdapterCache[row.Row_id] = adapter;
            }
            return adapter;
        }

        /// <summary>
        /// VOICE → VoiceRowAdapter 캐시 조회/생성.
        /// </summary>
        private static VoiceRowAdapter GetOrCreateVoiceAdapter(VOICE row)
        {
            if (!_voiceAdapterCache.TryGetValue(row.Voice_id, out var adapter))
            {
                adapter = new VoiceRowAdapter(row);
                _voiceAdapterCache[row.Voice_id] = adapter;
            }
            return adapter;
        }

        /// <summary>
        /// Sound 어댑터 캐시를 클리어한다.
        /// TB_SOUND._OnAfterLoad()에서 호출됨.
        /// </summary>
        public static void ClearSoundAdapterCache()
        {
            _soundAdapterCache.Clear();
        }

        /// <summary>
        /// Voice 어댑터 캐시를 클리어한다.
        /// TB_VOICE._OnAfterLoad()에서 호출됨.
        /// </summary>
        public static void ClearVoiceAdapterCache()
        {
            _voiceAdapterCache.Clear();
        }

        /// <summary>
        /// 전체 어댑터 캐시를 클리어한다.
        /// </summary>
        public static void ClearAdapterCache()
        {
            _soundAdapterCache.Clear();
            _voiceAdapterCache.Clear();
        }
    }
}
