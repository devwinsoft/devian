// SSOT: skills/devian-unity/30-unity-components/17-sound-manager/SKILL.md

#nullable enable

using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Devian
{
    /// <summary>
    /// 테이블 기반 사운드 재생/풀/채널/쿨타임 관리자.
    /// - 테이블(TB_SOUND) 기반으로만 식별
    /// - 채널/풀 책임은 SoundChannel에 위임
    /// - SerializeField 의존 없이 런타임 생성
    /// </summary>
    public sealed class SoundManager : AutoSingleton<SoundManager>
    {
        // ====================================================================
        // Sound Table Registry
        // ====================================================================

        /// <summary>
        /// sound_id로 row를 조회하는 델리게이트.
        /// 프로젝트에서 TB_SOUND 컨테이너를 연결한다.
        /// </summary>
        public Func<string, ISoundRow?>? GetSoundRow { get; set; }

        /// <summary>
        /// key(게임 로딩 그룹)로 sound_id 목록을 조회하는 델리게이트.
        /// </summary>
        public Func<string, IEnumerable<string>>? GetSoundIdsByKey { get; set; }

        // ====================================================================
        // Channels
        // ====================================================================

        private readonly Dictionary<SoundChannelType, SoundChannel> _channels = new();

        protected override void Awake()
        {
            base.Awake();
            InitializeChannels();
        }

        private void InitializeChannels()
        {
            for (int i = 0; i < (int)SoundChannelType.Max; i++)
            {
                var type = (SoundChannelType)i;
                var channelGo = new GameObject($"Channel_{type}");
                channelGo.transform.SetParent(transform);
                var channel = channelGo.AddComponent<SoundChannel>();
                channel.Initialize(type, poolSize: type == SoundChannelType.Bgm ? 2 : 8);
                _channels[type] = channel;
            }
        }

        // ====================================================================
        // Loaded Clips Cache (bundle_key 기준)
        // ====================================================================

        private readonly Dictionary<string, AudioClip> _clipCache = new();
        private readonly HashSet<string> _loadedBundleKeys = new();
        private readonly HashSet<string> _loadedGameKeys = new();

        // ====================================================================
        // Loading Policy
        // ====================================================================

        /// <summary>
        /// 게임 로딩 그룹(key) 기준으로 사운드를 로드한다.
        /// </summary>
        public IEnumerator LoadByKey(string gameKey, Action<string>? onError = null)
        {
            if (_loadedGameKeys.Contains(gameKey))
            {
                yield break;
            }

            if (GetSoundIdsByKey == null)
            {
                onError?.Invoke("[SoundManager] GetSoundIdsByKey delegate not set.");
                yield break;
            }

            if (GetSoundRow == null)
            {
                onError?.Invoke("[SoundManager] GetSoundRow delegate not set.");
                yield break;
            }

            var soundIds = GetSoundIdsByKey(gameKey);
            if (soundIds == null)
            {
                _loadedGameKeys.Add(gameKey);
                yield break;
            }

            // bundle_key별로 그룹화
            var bundleGroups = new Dictionary<string, List<ISoundRow>>();
            foreach (var soundId in soundIds)
            {
                var row = GetSoundRow(soundId);
                if (row == null) continue;
                if (row.source != SoundSourceType.Bundle) continue;

                if (!bundleGroups.TryGetValue(row.bundle_key, out var list))
                {
                    list = new List<ISoundRow>();
                    bundleGroups[row.bundle_key] = list;
                }
                list.Add(row);
            }

            // 각 bundle_key에 대해 로드
            foreach (var kvp in bundleGroups)
            {
                var bundleKey = kvp.Key;
                if (_loadedBundleKeys.Contains(bundleKey)) continue;

                yield return LoadBundleAsync(bundleKey, kvp.Value, onError);
                _loadedBundleKeys.Add(bundleKey);
            }

            _loadedGameKeys.Add(gameKey);
        }

        private IEnumerator LoadBundleAsync(string bundleKey, List<ISoundRow> rows, Action<string>? onError)
        {
            // AssetManager.LoadBundleAssets로 번들 로드 (캐시에 등록됨)
            yield return AssetManager.LoadBundleAssets<AudioClip>(bundleKey);

            // row들의 path로 클립을 조회하여 캐시 등록
            int loadedCount = 0;
            foreach (var row in rows)
            {
                var assetName = ExtractAssetName(row.path);
                var clip = AssetManager.GetAsset<AudioClip>(assetName);
                if (clip != null)
                {
                    _clipCache[row.sound_id] = clip;
                    loadedCount++;
                }
                else
                {
                    onError?.Invoke($"[SoundManager] AudioClip not found for sound_id '{row.sound_id}', path '{row.path}'.");
                }
            }

            if (loadedCount == 0)
            {
                onError?.Invoke($"[SoundManager] No AudioClips loaded for bundle_key '{bundleKey}'.");
            }
        }

        /// <summary>
        /// 게임 로딩 그룹(key) 기준으로 사운드를 언로드한다.
        /// </summary>
        public void UnloadByKey(string gameKey)
        {
            if (!_loadedGameKeys.Contains(gameKey)) return;

            if (GetSoundIdsByKey == null || GetSoundRow == null) return;

            var soundIds = GetSoundIdsByKey(gameKey);
            if (soundIds == null) return;

            // 해당 key의 sound_id들을 캐시에서 제거
            foreach (var soundId in soundIds)
            {
                _clipCache.Remove(soundId);
            }

            _loadedGameKeys.Remove(gameKey);

            // Note: bundle_key 단위 언로드는 여러 gameKey가 공유할 수 있으므로
            // 여기서는 clip 캐시만 제거. 실제 Addressables Release는 별도 정책 필요.
        }

        // ====================================================================
        // Play API
        // ====================================================================

        /// <summary>
        /// sound_id로 사운드를 재생한다.
        /// </summary>
        public SoundPlay? Play(
            string soundId,
            float volume = 1f,
            int groupId = 0,
            Vector3? position = null,
            string? channelOverride = null)
        {
            if (GetSoundRow == null)
            {
                Log.Warn("[SoundManager] GetSoundRow delegate not set.");
                return null;
            }

            var row = GetSoundRow(soundId);
            if (row == null)
            {
                Log.Warn($"[SoundManager] Sound not found: {soundId}");
                return null;
            }

            // 클립 조회
            if (!_clipCache.TryGetValue(soundId, out var clip))
            {
                Log.Warn($"[SoundManager] AudioClip not loaded: {soundId}");
                return null;
            }

            // 채널 결정
            var channelName = channelOverride ?? row.channel;
            if (!TryParseChannel(channelName, out var channelType))
            {
                channelType = SoundChannelType.Effect;
            }

            if (!_channels.TryGetValue(channelType, out var channel))
            {
                Log.Warn($"[SoundManager] Channel not found: {channelType}");
                return null;
            }

            // 볼륨/피치 계산
            var effectiveVolume = volume * row.volume_scale;
            var pitch = 1f;
            if (row.pitch_min > 0f && row.pitch_max > 0f && row.pitch_min < row.pitch_max)
            {
                pitch = UnityEngine.Random.Range(row.pitch_min, row.pitch_max);
            }

            // 3D 위치
            var is3d = row.is3d && position.HasValue;

            return channel.Play(
                soundId,
                clip,
                effectiveVolume,
                row.loop,
                row.cooltime,
                fadeInSeconds: 0f,
                fadeOutSeconds: 0f,
                waitSeconds: 0f,
                groupId,
                pitch,
                is3d,
                position,
                row.area_close,
                row.area_far
            );
        }

        /// <summary>
        /// 특정 채널의 모든 사운드를 정지한다.
        /// </summary>
        public void StopChannel(SoundChannelType channelType)
        {
            if (_channels.TryGetValue(channelType, out var channel))
            {
                channel.StopAll();
            }
        }

        /// <summary>
        /// 특정 그룹의 사운드를 정지한다.
        /// </summary>
        public void StopByGroup(int groupId)
        {
            foreach (var channel in _channels.Values)
            {
                channel.StopByGroup(groupId);
            }
        }

        /// <summary>
        /// 모든 사운드를 정지한다.
        /// </summary>
        public void StopAll()
        {
            foreach (var channel in _channels.Values)
            {
                channel.StopAll();
            }
        }

        // ====================================================================
        // Volume Control
        // ====================================================================

        /// <summary>
        /// 특정 채널의 볼륨을 설정한다.
        /// </summary>
        public void SetChannelVolume(SoundChannelType channelType, float volume)
        {
            if (_channels.TryGetValue(channelType, out var channel))
            {
                channel.SetVolume(volume);
            }
        }

        /// <summary>
        /// 특정 채널의 음소거를 설정한다.
        /// </summary>
        public void SetChannelMute(SoundChannelType channelType, bool mute)
        {
            if (_channels.TryGetValue(channelType, out var channel))
            {
                channel.SetMute(mute);
            }
        }

        // ====================================================================
        // Helpers
        // ====================================================================

        private static string ExtractAssetName(string path)
        {
            if (string.IsNullOrEmpty(path)) return path;

            // 경로에서 파일명만 추출 (확장자 제거)
            var lastSlash = path.LastIndexOfAny(new[] { '/', '\\' });
            var name = lastSlash >= 0 ? path.Substring(lastSlash + 1) : path;

            var dot = name.LastIndexOf('.');
            return dot >= 0 ? name.Substring(0, dot) : name;
        }

        private static bool TryParseChannel(string channelName, out SoundChannelType channelType)
        {
            channelType = SoundChannelType.Effect;
            if (string.IsNullOrEmpty(channelName)) return false;

            switch (channelName.ToLowerInvariant())
            {
                case "bgm":
                    channelType = SoundChannelType.Bgm;
                    return true;
                case "effect":
                case "sfx":
                    channelType = SoundChannelType.Effect;
                    return true;
                case "ui":
                    channelType = SoundChannelType.Ui;
                    return true;
                case "voice":
                    channelType = SoundChannelType.Voice;
                    return true;
                default:
                    return false;
            }
        }

        // ====================================================================
        // Lifecycle
        // ====================================================================

        protected override void OnDestroy()
        {
            StopAll();
            _clipCache.Clear();
            _loadedBundleKeys.Clear();
            _loadedGameKeys.Clear();
            _channels.Clear();
            base.OnDestroy();
        }
    }
}
