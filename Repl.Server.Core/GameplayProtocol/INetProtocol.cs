using System.Diagnostics.CodeAnalysis;
using Google.Protobuf;
using Repl.Server.Core.NetBuffers;

namespace Repl.Server.Core.Network;

/// <summary>
/// Interface requires to Serialize Deserialize Protobuf messages.
/// </summary>
public interface INetProtocol
{
    public bool Deserialize(ushort opCode, ReadOnlySpan<byte> byteArray, [NotNullWhen(true)] out IMessage? message);
    public bool Serialize<T>(T message, [NotNullWhen(true)] out SendBuffer? writer) where T : IMessage<T>;
    public bool Serialize<T>(T message, Span<byte> writer) where T : IMessage<T>;
}