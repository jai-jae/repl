using Repl.Server.Core.ReplProtocol;
using Repl.Server.Game.ConnectionHandshake.States;

namespace Repl.Server.Game.ConnectionHandshake.HandshakeInfo;

 
public class InactiveChannelInfo
{
    public long ChannelId { get; init; }
    public byte[] AccessToken { get; init; }
    public byte[] ConnectionToken { get; init; }
    public Dictionary<long, BoundConnectionInfo> Connections { get; set; } = new();
    public HashSet<long> AcknowledgedConnections { get; set; } = new();
    public int RequiredConnections { get; set; } = 1;
    public DateTime CreatedAt { get; init; }
    public DateTime ExpiresAt { get; init; }
    public bool HasSentChannelReady { get; set; }
        
    public bool ValidateConnectionToken(byte[] token)
    {
        return token != null && token.Length == ConnectionToken.Length && 
               token.SequenceEqual(ConnectionToken);
    }

    public InactiveChannelInfo(long channelId, byte[] accessToken, byte[] connectionToken, DateTime createdAt, DateTime expiresAt, int requiredConnections = 3)
    {
        this.ChannelId = channelId;
        AccessToken = accessToken;
        ConnectionToken = connectionToken;
        CreatedAt = createdAt;
        ExpiresAt = expiresAt;
        RequiredConnections = requiredConnections;
    }
}
    
public class ActiveChannelInfo
{
    public long ChannelId { get; init; }
    public Dictionary<long, ReplTcpConnection> Connections { get; init; } = [];
    public byte[] ReconnectToken { get; init; } = [];
    public DateTime CreatedAt { get; init; }
        
    public bool ValidateReconnectToken(byte[] token)
    {
        return token.Length == ReconnectToken.Length && 
               token.SequenceEqual(ReconnectToken);
    }
}
