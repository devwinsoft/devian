// SSOT: skills/devian-unity/22-sound-system/16-sound-tables/SKILL.md
// partial 확장 파일 - Generated TB_VOICE에 IVoiceRow 어댑터 추가
// Generated TB_VOICE는 voice_id가 PK
// v10 변경: key_group 제거, key_bundle 중심 로드/언로드, is3d/distance 추가
// VOICE는 SOUND 테이블 미참조, 직접 clip 경로 사용

#nullable enable

using System;
using System.Collections.Generic;

namespace Devian.Domain.Sound
{
    /// <summary>
    /// TB_VOICE partial 확장.
    /// - key_bundle → rows 그룹 인덱스 (로드/언로드 단위)
    /// - IVoiceRow 어댑터 제공 (IAudioRowBase 구현)
    /// key_group은 제거됨 - 로드/언로드는 key_bundle 단위로만 수행.
    /// </summary>
    public static partial class TB_VOICE
    {
        // key_bundle → rows (로드/언로드 단위 인덱스)
        private static readonly Dictionary<string, List<VOICE>> _rowsByBundleKey = new();

        // 빈 리스트 (반환용)
        private static readonly IReadOnlyList<VOICE> _emptyVoiceList = Array.Empty<VOICE>();

        /// <summary>
        /// key_bundle로 rows를 조회한다 (로드/언로드 단위).
        /// </summary>
        public static IReadOnlyList<VOICE> GetRowsByBundleKey(string bundleKey)
        {
            if (string.IsNullOrEmpty(bundleKey)) return _emptyVoiceList;
            return _rowsByBundleKey.TryGetValue(bundleKey, out var list) ? list : _emptyVoiceList;
        }

        /// <summary>
        /// 인덱스를 빌드한다.
        /// _OnAfterLoad에서 호출됨.
        /// </summary>
        public static void BuildBundleIndices()
        {
            _rowsByBundleKey.Clear();

            foreach (var row in GetAll())
            {
                if (row == null) continue;

                // key_bundle 인덱스
                if (!string.IsNullOrEmpty(row.Key_bundle))
                {
                    if (!_rowsByBundleKey.TryGetValue(row.Key_bundle, out var bundleList))
                    {
                        bundleList = new List<VOICE>();
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
            _rowsByBundleKey.Clear();
        }

        /// <summary>
        /// AfterLoad 훅 구현 - 테이블 로드 직후 자동 호출됨.
        /// </summary>
        static partial void _OnAfterLoad()
        {
            // 어댑터 캐시 클리어
            SoundVoiceTableRegistry.ClearVoiceAdapterCache();
            // 인덱스 빌드
            BuildBundleIndices();
        }
    }

    /// <summary>
    /// VOICE → IVoiceRow 어댑터.
    /// Generated VOICE 클래스를 IVoiceRow 인터페이스로 래핑한다.
    /// IAudioRowBase를 구현하여 BaseAudioManager에서 공통 처리 가능.
    /// VOICE는 SOUND 테이블을 참조하지 않고 clip 경로를 직접 사용한다.
    /// key_group은 제거됨 - key_bundle만 사용.
    /// </summary>
    public sealed class VoiceRowAdapter : IVoiceRow
    {
        private readonly VOICE _row;

        public VoiceRowAdapter(VOICE row)
        {
            _row = row ?? throw new ArgumentNullException(nameof(row));
        }

        // IVoiceRow 고유
        public string voice_id => _row.Voice_id;
        public string key_bundle => _row.Key_bundle;

        // IAudioRowBase 상수 (VOICE defaults)
        public bool isBundle => true; // VOICE는 항상 Bundle
        public SoundChannelType channel => SoundChannelType.Voice; // VOICE는 항상 Voice
        public bool loop => false; // VOICE는 항상 비루프
        public float cooltime => _row.Cooltime;
        public bool is3d => _row.Is3d;
        public float distance_near => _row.Distance_near;
        public float distance_far => _row.Distance_far;
        public float volume_scale => _row.Volume_scale;
        public float pitch_min => _row.Pitch_min;
        public float pitch_max => _row.Pitch_max;

        /// <summary>
        /// 컬럼명으로 clip 경로를 조회한다 (Resolve 단계에서만 호출).
        /// reflection 금지, 지원 언어 컬럼만 switch로 매칭.
        /// VOICE는 SOUND를 참조하지 않고 clip 경로를 직접 반환한다.
        /// 중국어는 clip_Chinese로 통합 (간체/번체 구분 없음).
        /// </summary>
        public bool TryGetClipColumn(string columnName, out string clipPath)
        {
            clipPath = string.Empty;

            switch (columnName)
            {
                case "clip_Korean":
                    clipPath = _row.Clip_Korean;
                    return !string.IsNullOrEmpty(clipPath);

                case "clip_English":
                    clipPath = _row.Clip_English;
                    return !string.IsNullOrEmpty(clipPath);

                case "clip_Japanese":
                    clipPath = _row.Clip_Japanese;
                    return !string.IsNullOrEmpty(clipPath);

                case "clip_Chinese":
                    clipPath = _row.Clip_Chinese;
                    return !string.IsNullOrEmpty(clipPath);

                default:
                    return false;
            }
        }
    }
}
