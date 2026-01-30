// SSOT: skills/devian-unity/30-unity-components/16-sound-tables/SKILL.md
// partial 확장 파일 - Generated TB_VOICE에 IVoiceRow 어댑터 추가
// Generated TB_VOICE는 voice_id가 PK

#nullable enable

using System;
using System.Collections.Generic;

namespace Devian.Domain.Sound
{
    /// <summary>
    /// TB_VOICE partial 확장.
    /// - group_key → rows 그룹 인덱스
    /// - IVoiceRow 어댑터 제공
    /// </summary>
    public static partial class TB_VOICE
    {
        // group_key → rows (게임 로딩 그룹 인덱스)
        private static readonly Dictionary<string, List<VOICE>> _rowsByGroupKey = new();

        // 빈 리스트 (반환용)
        private static readonly IReadOnlyList<VOICE> _emptyVoiceList = Array.Empty<VOICE>();

        /// <summary>
        /// 게임 로딩 그룹(group_key)으로 rows를 조회한다.
        /// </summary>
        public static IReadOnlyList<VOICE> GetRowsByGroupKey(string groupKey)
        {
            if (string.IsNullOrEmpty(groupKey)) return _emptyVoiceList;
            return _rowsByGroupKey.TryGetValue(groupKey, out var list) ? list : _emptyVoiceList;
        }

        /// <summary>
        /// 그룹 인덱스를 빌드한다.
        /// _OnAfterLoad에서 호출됨.
        /// </summary>
        public static void BuildGroupIndices()
        {
            _rowsByGroupKey.Clear();

            foreach (var row in GetAll())
            {
                if (row == null) continue;

                // group_key 인덱스
                if (!string.IsNullOrEmpty(row.Group_key))
                {
                    if (!_rowsByGroupKey.TryGetValue(row.Group_key, out var groupList))
                    {
                        groupList = new List<VOICE>();
                        _rowsByGroupKey[row.Group_key] = groupList;
                    }
                    groupList.Add(row);
                }
            }
        }

        /// <summary>
        /// 그룹 인덱스를 초기화한다.
        /// </summary>
        public static void ClearGroupIndices()
        {
            _rowsByGroupKey.Clear();
        }

        /// <summary>
        /// AfterLoad 훅 구현 - 테이블 로드 직후 자동 호출됨.
        /// </summary>
        static partial void _OnAfterLoad()
        {
            // 어댑터 캐시 클리어
            SoundVoiceTableRegistry.ClearVoiceAdapterCache();
            // 그룹 인덱스 빌드
            BuildGroupIndices();
        }
    }

    /// <summary>
    /// VOICE → IVoiceRow 어댑터.
    /// Generated VOICE 클래스를 IVoiceRow 인터페이스로 래핑한다.
    /// </summary>
    public sealed class VoiceRowAdapter : IVoiceRow
    {
        private readonly VOICE _row;

        public VoiceRowAdapter(VOICE row)
        {
            _row = row ?? throw new ArgumentNullException(nameof(row));
        }

        public string voice_id => _row.Voice_id;
        // text_l10n_key 제거됨 - 자막 키가 필요하면 voice_id 자체를 사용
        public string speaker => _row.Speaker;
        public string category => _row.Category;
        public int priority => _row.Priority;
        public string group_key => _row.Group_key;
        public float cooltime => _row.Cooltime;

        /// <summary>
        /// 컬럼명으로 sound_id를 조회한다 (Resolve 단계에서만 호출).
        /// reflection 금지, 지원 언어 컬럼만 switch로 매칭.
        /// </summary>
        public bool TryGetClipColumn(string columnName, out string soundId)
        {
            soundId = string.Empty;

            switch (columnName)
            {
                case "clip_Korean":
                    soundId = _row.Clip_Korean;
                    return !string.IsNullOrEmpty(soundId);

                case "clip_English":
                    soundId = _row.Clip_English;
                    return !string.IsNullOrEmpty(soundId);

                case "clip_Japanese":
                    soundId = _row.Clip_Japanese;
                    return !string.IsNullOrEmpty(soundId);

                case "clip_ChineseSimplified":
                    soundId = _row.Clip_ChineseSimplified;
                    return !string.IsNullOrEmpty(soundId);

                case "clip_ChineseTraditional":
                    soundId = _row.Clip_ChineseTraditional;
                    return !string.IsNullOrEmpty(soundId);

                default:
                    return false;
            }
        }
    }
}
