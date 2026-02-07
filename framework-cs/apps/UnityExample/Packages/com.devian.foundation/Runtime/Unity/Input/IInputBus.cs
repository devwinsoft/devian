using System;

namespace Devian
{
    /// <summary>
    /// InputFrame 발행/구독 버스.
    /// </summary>
    public interface IInputBus
    {
        /// <summary>
        /// 프레임을 모든 구독자에게 발행한다.
        /// </summary>
        void Publish(InputFrame frame);

        /// <summary>
        /// 핸들러를 등록하고 토큰을 반환한다.
        /// </summary>
        int Subscribe(Action<InputFrame> handler);

        /// <summary>
        /// 토큰으로 핸들러를 해제한다.
        /// </summary>
        void Unsubscribe(int token);
    }
}
