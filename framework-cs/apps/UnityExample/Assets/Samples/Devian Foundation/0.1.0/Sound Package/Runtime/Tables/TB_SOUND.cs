// SSOT: skills/devian-unity/22-sound-system/16-sound-tables/SKILL.md
// partial 확장 파일 - Generated TB_SOUND에 sound_id/key_bundle 그룹 인덱스 추가
// row_id가 PK, sound_id는 논리 그룹 키 (중복 허용)
// v10 변경: key_group 제거, key_bundle 중심 로드/언로드, isBundle(bool), channel(enum)

#nullable enable

using System;
using System.Collections.Generic;

namespace Devian.Domain.Sound
{
    /// <summary>
    /// TB_SOUND partial 확장.
    /// - sound_id → rows 그룹 인덱스
    /// - key_bundle → rows 그룹 인덱스 (로드/언로드 단위)
    /// - ISoundRow 어댑터 제공
    /// key_group은 제거됨 - 로드/언로드는 key_bundle 단위로만 수행.
    /// </summary>
    public static partial class TB_SOUND
    {
        // sound_id → rows (그룹 인덱스)
        private static readonly Dictionary<string, List<SOUND>> _rowsBySoundId = new();

        // key_bundle → rows (로드/언로드 단위 인덱스)
        private static readonly Dictionary<string, List<SOUND>> _rowsByBundleKey = new();

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
        /// key_bundle로 rows를 조회한다 (로드/언로드 단위).
        /// </summary>
        public static IReadOnlyList<SOUND> GetRowsByBundleKey(string bundleKey)
        {
            if (string.IsNullOrEmpty(bundleKey)) return _emptyList;
            return _rowsByBundleKey.TryGetValue(bundleKey, out var list) ? list : _emptyList;
        }

        /// <summary>
        /// 인덱스를 빌드한다.
        /// _OnAfterLoad에서 호출됨.
        /// </summary>
        public static void BuildBundleIndices()
        {
            _rowsBySoundId.Clear();
            _rowsByBundleKey.Clear();

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

                // key_bundle 인덱스
                if (!string.IsNullOrEmpty(row.Key_bundle))
                {
                    if (!_rowsByBundleKey.TryGetValue(row.Key_bundle, out var bundleList))
                    {
                        bundleList = new List<SOUND>();
                        _rowsByBundleKey[row.Key_bundle] = bundleList;
                    }
                    bundleList.Add(row);
                }
            }
        }

        /// <summary>
        /// 인덱스를 초기화한다.
        /// </summary>
        public static void ClearBundleIndices()
        {
            _rowsBySoundId.Clear();
            _rowsByBundleKey.Clear();
        }

        /// <summary>
        /// AfterLoad 훅 구현 - 테이블 로드 직후 자동 호출됨.
        /// </summary>
        static partial void _OnAfterLoad()
        {
            // 어댑터 캐시 클리어
            SoundVoiceTableRegistry.ClearSoundAdapterCache();
            // 인덱스 빌드
            BuildBundleIndices();
        }
    }

    /// <summary>
    /// SOUND → ISoundRow 어댑터.
    /// Generated SOUND 클래스를 ISoundRow 인터페이스로 래핑한다.
    /// IAudioRowBase를 구현하여 BaseAudioManager에서 공통 처리 가능.
    /// key_group은 제거됨 - key_bundle만 사용.
    /// </summary>
    public sealed class SoundRowAdapter : ISoundRow
    {
        private readonly SOUND _row;

        public SoundRowAdapter(SOUND row)
        {
            _row = row ?? throw new ArgumentNullException(nameof(row));
        }

        // ISoundRow 고유
        public int row_id => _row.Row_id;
        public string sound_id => _row.Sound_id;
        public bool isBundle => _row.IsBundle;
        public string key_bundle => _row.Key_bundle;
        public string path => _row.Path;
        public SoundChannelType channel => ParseChannel(_row.Channel);
        public int weight => _row.Weight;

        // IAudioRowBase 공통
        public bool loop => _row.Loop;
        public float cooltime => _row.Cooltime;
        public bool is3d => _row.Is3d;
        public float distance_near => _row.Distance_near;
        public float distance_far => _row.Distance_far;
        public float volume_scale => _row.Volume_scale;
        public float pitch_min => _row.Pitch_min;
        public float pitch_max => _row.Pitch_max;

        private static SoundChannelType ParseChannel(string channel)
        {
            if (string.IsNullOrEmpty(channel))
                return SoundChannelType.Effect;

            if (Enum.TryParse<SoundChannelType>(channel, true, out var result))
                return result;

            return SoundChannelType.Effect; // default
        }
    }
}
