// SSOT: skills/devian-unity/30-unity-components/17-sound-manager/SKILL.md

#nullable enable

namespace Devian
{
    /// <summary>
    /// 오디오 에셋 경로에서 에셋 이름을 추출하는 유틸리티.
    /// SoundManager/VoiceManager 공통으로 사용한다.
    /// </summary>
    internal static class AudioAssetNameUtil
    {
        /// <summary>
        /// 경로에서 에셋 이름을 추출한다.
        /// 예: "Audio/Voice/Korean/hello.wav" → "hello"
        /// </summary>
        /// <param name="path">에셋 경로</param>
        /// <returns>확장자를 제외한 에셋 이름</returns>
        public static string ExtractAssetName(string path)
        {
            if (string.IsNullOrEmpty(path)) return path;

            // 마지막 슬래시 이후
            var lastSlash = path.LastIndexOfAny(new[] { '/', '\\' });
            var name = lastSlash >= 0 ? path.Substring(lastSlash + 1) : path;

            // 확장자 제거
            var dot = name.LastIndexOf('.');
            return dot >= 0 ? name.Substring(0, dot) : name;
        }

        /// <summary>
        /// Resources 경로에서 확장자를 제거한다.
        /// 예: "Audio/BGM/theme.wav" → "Audio/BGM/theme"
        /// </summary>
        /// <param name="path">Resources 경로</param>
        /// <returns>확장자를 제외한 경로</returns>
        public static string RemoveExtension(string path)
        {
            if (string.IsNullOrEmpty(path)) return path;

            var dot = path.LastIndexOf('.');
            if (dot < 0) return path;

            // 슬래시 이후에 점이 있는지 확인 (폴더명에 점이 있을 수 있음)
            var lastSlash = path.LastIndexOfAny(new[] { '/', '\\' });
            if (lastSlash >= 0 && dot < lastSlash)
            {
                return path; // 확장자가 아님
            }

            return path.Substring(0, dot);
        }
    }
}
