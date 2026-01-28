// SSOT: skills/devian-unity/30-unity-components/16-sound-tables/SKILL.md
// 수기 유지 파일 (Generated 아님)

#nullable enable

using System;
using System.Collections.Generic;
using UnityEngine;

namespace Devian.Domain.Game
{
    /// <summary>
    /// TB_SOUND row. 재생 단위(실제 AudioClip)의 정본.
    /// ISoundRow 인터페이스를 구현하여 SoundManager에서 사용한다.
    /// </summary>
    public sealed class TB_SOUND_Row : ISoundRow
    {
        // 필수 컬럼
        public string sound_id { get; set; } = string.Empty;
        public int row_id { get; set; }
        public string key { get; set; } = string.Empty;
        public SoundSourceType source { get; set; }
        public string bundle_key { get; set; } = string.Empty;
        public string path { get; set; } = string.Empty;
        public string channel { get; set; } = string.Empty;
        public bool loop { get; set; }
        public float cooltime { get; set; }
        public bool is3d { get; set; }
        public float area_close { get; set; } = 1f;
        public float area_far { get; set; } = 500f;

        // 선택 컬럼
        public int weight { get; set; } = 1;
        public float volume_scale { get; set; } = 1f;
        public float pitch_min { get; set; }
        public float pitch_max { get; set; }
    }

    /// <summary>
    /// TB_SOUND 컨테이너. sound_id로 row를 조회한다.
    /// </summary>
    public static class TB_SOUND
    {
        private static readonly Dictionary<string, TB_SOUND_Row> _byId = new();
        private static readonly Dictionary<string, List<string>> _byKey = new();
        private static readonly List<TB_SOUND_Row> _allRows = new();

        /// <summary>
        /// sound_id로 row를 조회한다.
        /// </summary>
        public static TB_SOUND_Row? Get(string soundId)
        {
            return _byId.TryGetValue(soundId, out var row) ? row : null;
        }

        /// <summary>
        /// 게임 로딩 그룹(key)로 sound_id 목록을 조회한다.
        /// </summary>
        public static IEnumerable<string> GetIdsByKey(string key)
        {
            if (_byKey.TryGetValue(key, out var list))
            {
                return list;
            }
            return Array.Empty<string>();
        }

        /// <summary>
        /// 모든 row를 순회한다.
        /// </summary>
        public static IEnumerable<TB_SOUND_Row> All() => _allRows;

        /// <summary>
        /// row를 추가한다.
        /// </summary>
        public static void Insert(TB_SOUND_Row row)
        {
            if (row == null) return;

            _byId[row.sound_id] = row;
            _allRows.Add(row);

            // key 인덱싱
            if (!string.IsNullOrEmpty(row.key))
            {
                if (!_byKey.TryGetValue(row.key, out var list))
                {
                    list = new List<string>();
                    _byKey[row.key] = list;
                }
                list.Add(row.sound_id);
            }
        }

        /// <summary>
        /// 모든 데이터를 초기화한다.
        /// </summary>
        public static void Clear()
        {
            _byId.Clear();
            _byKey.Clear();
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
            // 예: Newtonsoft.Json 사용
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
