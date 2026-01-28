// SSOT: skills/devian-unity/30-unity-components/17-sound-manager/SKILL.md
// 수기 유지 파일 (Generated 아님)
// Sound/Voice 테이블과 Manager를 연결하는 레지스트리.

#nullable enable

using System.Linq;
using UnityEngine;

namespace Devian.Domain.Game
{
    /// <summary>
    /// Sound/Voice 테이블과 Manager를 연결한다.
    /// - TB_SOUND → SoundManager.GetSoundRow
    /// - TB_VOICE → VoiceManager.GetVoiceRow
    /// </summary>
    internal static class SoundVoiceTableRegistry
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Register()
        {
            // SoundManager 연결
            SoundManager.Instance.GetSoundRow = (soundId) => TB_SOUND.Get(soundId);
            SoundManager.Instance.GetSoundIdsByKey = (key) => TB_SOUND.GetIdsByKey(key);

            // VoiceManager 연결
            VoiceManager.Instance.GetVoiceRow = (voiceId) => TB_VOICE.Get(voiceId);
            VoiceManager.Instance.GetAllVoiceRows = () => TB_VOICE.All().Cast<IVoiceRow>();

            // TableManager에 TB 로더 등록
            global::Devian.TableManager.Instance.RegisterTbLoader("SOUND", (format, text, bin) =>
            {
                if (format == global::Devian.TableFormat.Json && text != null)
                    TB_SOUND.LoadFromNdjson(text);
                else if (format == global::Devian.TableFormat.Pb64 && bin != null)
                    TB_SOUND.LoadFromPb64Binary(bin);
            });

            global::Devian.TableManager.Instance.RegisterTbLoader("VOICE", (format, text, bin) =>
            {
                if (format == global::Devian.TableFormat.Json && text != null)
                    TB_VOICE.LoadFromNdjson(text);
                else if (format == global::Devian.TableFormat.Pb64 && bin != null)
                    TB_VOICE.LoadFromPb64Binary(bin);
            });
        }
    }
}
