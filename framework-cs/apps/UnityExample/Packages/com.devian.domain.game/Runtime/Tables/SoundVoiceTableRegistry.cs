// SSOT: skills/devian-unity/30-unity-components/17-sound-manager/SKILL.md
// 수기 유지 파일 (Generated 아님)
// Sound/Voice 테이블과 Manager를 연결하는 레지스트리.
// Generated 클래스(SOUND, VOICE)를 Adapter로 ISoundRow/IVoiceRow 인터페이스에 맞춘다.

#nullable enable

using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Devian.Domain.Game
{
    /// <summary>
    /// Sound/Voice 테이블과 Manager를 연결한다.
    /// - TB_SOUND → SoundManager.GetSoundRowsBySoundId / GetSoundRowsByKey
    /// - TB_VOICE → VoiceManager.GetVoiceRow / GetAllVoiceRows
    /// - Generated 클래스를 Adapter로 ISoundRow/IVoiceRow 인터페이스에 맞춘다.
    /// </summary>
    internal static class SoundVoiceTableRegistry
    {
        // Adapter 캐시 (SOUND/VOICE → ISoundRow/IVoiceRow)
        private static readonly Dictionary<int, SoundRowAdapter> _soundAdapterCache = new();
        private static readonly Dictionary<string, VoiceRowAdapter> _voiceAdapterCache = new();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Register()
        {
            // SoundManager 연결 (새 API: row_id PK, sound_id 그룹 키)
            SoundManager.Instance.GetSoundRowsBySoundId = GetSoundRowsBySoundId;
            SoundManager.Instance.GetSoundRowsByKey = GetSoundRowsByKey;

            // VoiceManager 연결
            VoiceManager.Instance.GetVoiceRow = GetVoiceRow;
            VoiceManager.Instance.GetAllVoiceRows = GetAllVoiceRows;
            VoiceManager.Instance.GetVoiceRowsByGroupKey = GetVoiceRowsByGroupKey;

            // TableManager에 TB 로더 등록
            global::Devian.TableManager.Instance.RegisterTbLoader("SOUND", (format, text, bin) =>
            {
                // 어댑터 캐시 클리어
                _soundAdapterCache.Clear();

                if (format == global::Devian.TableFormat.Json && text != null)
                    TB_SOUND.LoadFromNdjson(text);
                else if (format == global::Devian.TableFormat.Pb64 && bin != null)
                    TB_SOUND.LoadFromPb64Binary(bin);

                // 로드 후 그룹 인덱스 빌드
                TB_SOUND.BuildGroupIndices();
            });

            global::Devian.TableManager.Instance.RegisterTbLoader("VOICE", (format, text, bin) =>
            {
                // 어댑터 캐시 클리어
                _voiceAdapterCache.Clear();

                if (format == global::Devian.TableFormat.Json && text != null)
                    TB_VOICE.LoadFromNdjson(text);
                else if (format == global::Devian.TableFormat.Pb64 && bin != null)
                    TB_VOICE.LoadFromPb64Binary(bin);

                // 로드 후 그룹 인덱스 빌드
                TB_VOICE.BuildGroupIndices();
            });
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
        /// key로 rows를 조회하고 ISoundRow 어댑터로 래핑한다.
        /// </summary>
        private static IReadOnlyList<ISoundRow> GetSoundRowsByKey(string key)
        {
            var rows = TB_SOUND.GetRowsByKey(key);
            if (rows.Count == 0) return System.Array.Empty<ISoundRow>();

            var result = new List<ISoundRow>(rows.Count);
            foreach (var row in rows)
            {
                result.Add(GetOrCreateSoundAdapter(row));
            }
            return result;
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
        /// group_key로 voice rows를 조회하고 IVoiceRow 어댑터로 래핑한다.
        /// </summary>
        private static IEnumerable<IVoiceRow> GetVoiceRowsByGroupKey(string groupKey)
        {
            var rows = TB_VOICE.GetRowsByGroupKey(groupKey);
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
        /// 어댑터 캐시를 클리어한다.
        /// </summary>
        public static void ClearAdapterCache()
        {
            _soundAdapterCache.Clear();
            _voiceAdapterCache.Clear();
        }
    }
}
