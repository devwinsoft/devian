// SSOT: skills/devian-unity/30-unity-components/16-sound-tables/SKILL.md
// partial 확장 파일 - Generated TB_SOUND에 sound_id/key 그룹 인덱스 추가
// row_id가 PK, sound_id는 논리 그룹 키 (중복 허용)

#nullable enable

using System;
using System.Collections.Generic;

namespace Devian.Domain.Game
{
    /// <summary>
    /// TB_SOUND partial 확장.
    /// - sound_id → rows 그룹 인덱스
    /// - key → rows 그룹 인덱스
    /// - ISoundRow 어댑터 제공
    /// </summary>
    public static partial class TB_SOUND
    {
        // sound_id → rows (그룹 인덱스)
        private static readonly Dictionary<string, List<SOUND>> _rowsBySoundId = new();

        // key → rows (게임 로딩 그룹 인덱스)
        private static readonly Dictionary<string, List<SOUND>> _rowsByKey = new();

        // 빈 리스트 (반환용)
        private static readonly IReadOnlyList<SOUND> _emptyList = Array.Empty<SOUND>();

        /// <summary>
        /// row_id로 단일 row를 조회한다 (PK 조회).
        /// Generated의 Get(int)과 동일.
        /// </summary>
        public static SOUND? GetByRowId(int rowId)
        {
            return Get(rowId);
        }

        /// <summary>
        /// sound_id로 후보 rows를 조회한다 (그룹 조회).
        /// 동일 sound_id에 여러 row가 있을 수 있다.
        /// </summary>
        public static IReadOnlyList<SOUND> GetRows(string soundId)
        {
            if (string.IsNullOrEmpty(soundId)) return _emptyList;
            return _rowsBySoundId.TryGetValue(soundId, out var list) ? list : _emptyList;
        }

        /// <summary>
        /// 게임 로딩 그룹(key)로 rows를 조회한다.
        /// </summary>
        public static IReadOnlyList<SOUND> GetRowsByKey(string key)
        {
            if (string.IsNullOrEmpty(key)) return _emptyList;
            return _rowsByKey.TryGetValue(key, out var list) ? list : _emptyList;
        }

        /// <summary>
        /// 그룹 인덱스를 빌드한다.
        /// LoadFromJson/LoadFromNdjson 호출 후 반드시 호출해야 한다.
        /// </summary>
        public static void BuildGroupIndices()
        {
            _rowsBySoundId.Clear();
            _rowsByKey.Clear();

            foreach (var row in GetAll())
            {
                if (row == null) continue;

                // sound_id 그룹 인덱스
                if (!string.IsNullOrEmpty(row.Sound_id))
                {
                    if (!_rowsBySoundId.TryGetValue(row.Sound_id, out var soundIdList))
                    {
                        soundIdList = new List<SOUND>();
                        _rowsBySoundId[row.Sound_id] = soundIdList;
                    }
                    soundIdList.Add(row);
                }

                // key 인덱스
                if (!string.IsNullOrEmpty(row.Key))
                {
                    if (!_rowsByKey.TryGetValue(row.Key, out var keyList))
                    {
                        keyList = new List<SOUND>();
                        _rowsByKey[row.Key] = keyList;
                    }
                    keyList.Add(row);
                }
            }
        }

        /// <summary>
        /// 그룹 인덱스를 초기화한다.
        /// </summary>
        public static void ClearGroupIndices()
        {
            _rowsBySoundId.Clear();
            _rowsByKey.Clear();
        }
    }

    /// <summary>
    /// SOUND → ISoundRow 어댑터.
    /// Generated SOUND 클래스를 ISoundRow 인터페이스로 래핑한다.
    /// </summary>
    public sealed class SoundRowAdapter : ISoundRow
    {
        private readonly SOUND _row;

        public SoundRowAdapter(SOUND row)
        {
            _row = row ?? throw new ArgumentNullException(nameof(row));
        }

        public int row_id => _row.Row_id;
        public string sound_id => _row.Sound_id;
        public string key => _row.Key;
        public SoundSourceType source => ParseSource(_row.Source);
        public string bundle_key => _row.Bundle_key;
        public string path => _row.Path;
        public string channel => _row.Channel;
        public bool loop => _row.Loop;
        public float cooltime => _row.Cooltime;
        public bool is3d => _row.Is3d;
        public float area_close => _row.Area_close;
        public float area_far => _row.Area_far;
        public int weight => _row.Weight;
        public float volume_scale => _row.Volume_scale;
        public float pitch_min => _row.Pitch_min;
        public float pitch_max => _row.Pitch_max;

        private static SoundSourceType ParseSource(string source)
        {
            if (string.Equals(source, "Bundle", StringComparison.OrdinalIgnoreCase))
                return SoundSourceType.Bundle;
            if (string.Equals(source, "Resource", StringComparison.OrdinalIgnoreCase))
                return SoundSourceType.Resource;
            return SoundSourceType.Bundle; // default
        }
    }
}
