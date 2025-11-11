using Repl.Server.Core.ReplProtocol;
using Repl.Server.Game.ConnectionHandshake.Protocol;

namespace Repl.Server.Game.ConnectionHandshake.Jobs;

internal class ConnectionHandshakeJob
{
    public HandshakeManagerJobType Type { get; init; }
    public ReplTcpConnection Connection { get; init; }
    public long ConnectionId { get; init; }
    public NetChannelOpCode OpCode { get; init; }
    public ReadOnlyMemory<byte> Message { get; init; }
    public DateTime Timestamp { get; init; }
}