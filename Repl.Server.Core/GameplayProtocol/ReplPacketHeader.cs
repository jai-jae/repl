using System.Buffers.Binary;

namespace Repl.Server.Core.ReplProtocol;

/// <summary>
///         0             1               2               3               4
///         0 1 2 3 4 5 6 7 8 9 0 1 2 3 4 5 6 7 8 9 0 1 2 3 4 5 6 7 8 9 0 1
///        +-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+
///        |        TotalPacketSize        |       MessageOpCode           |
///        +-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+
///        |                                                               :
///        :                             Data                              :
///        :                                                               |
///        +-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+
/// </summary>
public static class ReplPacketHeader
{
    public const int MESSAGE_SIZE_HEADER_OFFSET = sizeof(ushort);
    public const int OPCODE_HEADER_OFFSET = sizeof(ushort);
    public const int HEADER_SIZE = MESSAGE_SIZE_HEADER_OFFSET + OPCODE_HEADER_OFFSET;

    /// <summary>
    /// Parses the total packet size from the header using Little Endian byte order.
    /// </summary>
    public static ushort ParsePacketSize(ReadOnlySpan<byte> header)
    {
        return BinaryPrimitives.ReadUInt16LittleEndian(header);
    }

    /// <summary>
    /// Parses the operation code from the header using Little Endian byte order.
    /// </summary>
    public static ushort ParseOpCode(ReadOnlySpan<byte> header)
    {
        return BinaryPrimitives.ReadUInt16LittleEndian(header.Slice(MESSAGE_SIZE_HEADER_OFFSET));
    }

    /// <summary>
    /// Writes the total packet size and operation code to the header using Little Endian byte order.
    /// </summary>
    public static void WriteHeader(Span<byte> header, ushort packetSize, ushort opCode)
    {
        BinaryPrimitives.WriteUInt16LittleEndian(header, packetSize);
        BinaryPrimitives.WriteUInt16LittleEndian(header.Slice(MESSAGE_SIZE_HEADER_OFFSET), opCode);
    }
}