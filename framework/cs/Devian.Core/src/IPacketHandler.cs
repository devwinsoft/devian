using System.Threading.Tasks;

namespace Devian.Core
{
    /// <summary>
    /// Packet handler interface for protocol messages.
    /// </summary>
    public interface IPacketHandler
    {
        ushort Opcode { get; }
        Task HandleAsync(PacketEnvelope envelope);
    }

    public interface IPacketHandler<TPacket> : IPacketHandler
    {
        Task HandleAsync(TPacket packet, PacketEnvelope envelope);
    }
}
