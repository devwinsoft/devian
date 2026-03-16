using System;
using UnityEngine;

namespace Devian
{
    public enum AnimPlayCount
    {
        Loop = 0,
        One = 1,
        Two = 2,
        Three = 3,
        Four = 4,
        Five = 5,
        Six = 6,
        Seven = 7,
        Eight = 8,
        Nine = 9,
        Ten = 10,
    }

    [Serializable]
    public sealed class AnimSequenceStep
    {
        // 규약: Animator stateName == Clip.name
        public AnimationClip Clip;

        public float Speed = 1f;

        // Loop는 무한 반복(자동 다음 스텝 전환 없음)
        public AnimPlayCount Repeat = AnimPlayCount.One;

        // 다음 스텝으로 넘어갈 때 사용할 크로스페이드 시간
        public float FadeTime = 0f;
    }

    // ScriptableObject 금지: embedded data로 사용
    [Serializable]
    public sealed class AnimSequenceData
    {
        public AnimSequenceStep[] Steps;

        public bool IsValid()
        {
            return Steps != null && Steps.Length > 0;
        }
    }
}
