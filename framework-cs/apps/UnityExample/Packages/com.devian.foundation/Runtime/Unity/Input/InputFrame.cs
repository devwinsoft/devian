using UnityEngine;

namespace Devian
{
    /// <summary>
    /// 한 프레임의 정규화된 입력 스냅샷.
    /// Move/Look은 raw Vector2, ButtonBits는 ulong bitset (최대 64 버튼).
    /// </summary>
    public readonly struct InputFrame
    {
        public readonly Vector2 Move;
        public readonly Vector2 Look;
        public readonly ulong ButtonBits;
        public readonly InputContext Context;
        public readonly float Timestamp;

        public InputFrame(Vector2 move, Vector2 look, ulong buttonBits, InputContext context, float timestamp)
        {
            Move = move;
            Look = look;
            ButtonBits = buttonBits;
            Context = context;
            Timestamp = timestamp;
        }

        /// <summary>
        /// 지정 index의 버튼이 눌려 있으면 true.
        /// 0..63 범위 밖이면 false.
        /// </summary>
        public bool IsDown(int buttonIndex)
        {
            if ((uint)buttonIndex >= 64u) return false;
            return (ButtonBits & (1UL << buttonIndex)) != 0;
        }
    }
}
