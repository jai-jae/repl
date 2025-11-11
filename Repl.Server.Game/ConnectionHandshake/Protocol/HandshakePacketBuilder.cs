using Repl.Server.Core.NetBuffers;
using Repl.Server.Core.ReplProtocol;

namespace Repl.Server.Game.ConnectionHandshake.Protocol;

public static class HandshakePacketBuilder
{
    public static SendBuffer CreatePacket<T>(ushort opCode, T message) where T : IHandshakeMessage
    {
        int messageSize = message.GetSize();
        int requiredBufferSize = ReplPacketHeader.HEADER_SIZE + messageSize;
        
        ArgumentOutOfRangeException.ThrowIfGreaterThan(requiredBufferSize, 4096);

        var totalPacketSize = (ushort)requiredBufferSize;
        
        SendBuffer buffer = SendBuffer.Rent(totalPacketSize);
        ReplPacketHeader.WriteHeader(buffer.WriteSegment, totalPacketSize, opCode);
        message.WriteTo(buffer.WriteSegment.Slice(ReplPacketHeader.HEADER_SIZE));

        return buffer;
    }
    
    
}


