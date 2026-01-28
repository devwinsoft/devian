// SSOT: skills/devian-unity/30-unity-components/16-sound-tables/SKILL.md
// 수기 유지 파일 (Generated 아님)

#nullable enable

using System;
using System.Collections.Generic;
using UnityEngine;

namespace Devian.Domain.Game
{
    /// <summary>
    /// TB_VOICE row. voice_id 중심의 논리 정의 + 모든 지원 언어 clip 매핑.
    /// IVoiceRow 인터페이스를 구현하여 VoiceManager에서 사용한다.
    /// </summary>
    public sealed class TB_VOICE_Row : IVoiceRow
    {
        // 필수 컬럼
        public string voice_id { get; set; } = string.Empty;
        public string text_l10n_key { get; set; } = string.Empty;

        // 선택 컬럼
        public string speaker { get; set; } = string.Empty;
        public string category { get; set; } = string.Empty;
        public int priority { get; set; }
        public string group_key { get; set; } = string.Empty;
        public float cooltime { get; set; }

        // 언어별 컬럼 (SystemLanguage.ToString() 규칙)
        // clip 값은 TB_SOUND.sound_id를 참조한다.
        public string clip_Korean { get; set; } = string.Empty;
        public string clip_English { get; set; } = string.Empty;
        public string clip_Japanese { get; set; } = string.Empty;
        public string clip_ChineseSimplified { get; set; } = string.Empty;
        public string clip_ChineseTraditional { get; set; } = string.Empty;

        /// <summary>
        /// 지정된 언어에 해당하는 sound_id를 반환한다.
        /// </summary>
        public string GetSoundIdForLanguage(SystemLanguage language)
        {
            return language switch
            {
                SystemLanguage.Korean => clip_Korean,
                SystemLanguage.English => clip_English,
                SystemLanguage.Japanese => clip_Japanese,
                SystemLanguage.ChineseSimplified => clip_ChineseSimplified,
                SystemLanguage.ChineseTraditional => clip_ChineseTraditional,
                _ => clip_English // fallback
            };
        }
    }

    /// <summary>
    /// TB_VOICE 컨테이너. voice_id로 row를 조회한다.
    /// 언어별로 테이블을 쪼개지 않는다 (Hard Rule).
    /// </summary>
    public static class TB_VOICE
    {
        private static readonly Dictionary<string, TB_VOICE_Row> _byId = new();
        private static readonly List<TB_VOICE_Row> _allRows = new();

        /// <summary>
        /// voice_id로 row를 조회한다.
        /// </summary>
        public static TB_VOICE_Row? Get(string voiceId)
        {
            return _byId.TryGetValue(voiceId, out var row) ? row : null;
        }

        /// <summary>
        /// 모든 row를 순회한다.
        /// </summary>
        public static IEnumerable<TB_VOICE_Row> All() => _allRows;

        /// <summary>
        /// row를 추가한다.
        /// </summary>
        public static void Insert(TB_VOICE_Row row)
        {
            if (row == null) return;

            _byId[row.voice_id] = row;
            _allRows.Add(row);
        }

        /// <summary>
        /// 모든 데이터를 초기화한다.
        /// </summary>
        public static void Clear()
        {
            _byId.Clear();
            _allRows.Clear();
        }

        /// <summary>
        /// 로드된 row 개수.
        /// </summary>
        public static int Count => _allRows.Count;

        /// <summary>
        /// NDJSON에서 로드한다.
        /// 프로젝트에서 구현을 제공해야 한다.
        /// </summary>
        public static void LoadFromNdjson(string ndjsonText)
        {
            // 프로젝트별 파싱 로직 구현
            throw new NotImplementedException("LoadFromNdjson must be implemented by the project.");
        }

        /// <summary>
        /// Pb64 바이너리에서 로드한다.
        /// 프로젝트에서 구현을 제공해야 한다.
        /// </summary>
        public static void LoadFromPb64Binary(byte[] pb64Binary)
        {
            // 프로젝트별 파싱 로직 구현
            throw new NotImplementedException("LoadFromPb64Binary must be implemented by the project.");
        }
    }
}
