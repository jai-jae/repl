using Microsoft.Extensions.Logging;
using Repl.Server.Core.Network.NetChannel;
using Repl.Server.Core.ReplProtocol;
using Repl.Server.Game.ConnectionHandshake.HandshakeInfo;
using Repl.Server.Game.ConnectionHandshake.Protocol;
using Repl.Server.Game.ConnectionHandshake.States;

namespace Repl.Server.Game.ConnectionHandshake;

public partial class ConnectionHandshakeManager
{ 
    private bool ValidateToken(byte[] accessToken) => accessToken.Length > 0;
        
        private HandshakeResult ProcessInit(ReplTcpConnection connection, ReadOnlyMemory<byte> message)
        {
            if (InitRequest.TryParse(message.Span, out var request) == false)
            {
                return HandshakeResult.Fail("Invalid init request", true);
            }
            
            // Check if connection already processed handshake
            if (this.TryFindConnection(connection.ConnectionId, out var connectionInfo) == false)
            {
                return HandshakeResult.Fail("Connection does not exist.", true);
            }
            
            if (connectionInfo.Value.Location != ConnectionLocation.Unbound)
            {
                return HandshakeResult.Fail("Connection not in unbound state", true);
            }
            
            if (connectionInfo.Value.HasProcessedHandshake)
            {
                return HandshakeResult.Fail("Duplicate Init", true);
            }
            
            if (this.ValidateToken(request.AccessToken) == false)
            {
                return HandshakeResult.Fail("Invalid token", true);
            }
            
            if (this.tokenToChannel.ContainsKey(request.AccessToken))
            {
                return HandshakeResult.Reject(NetChannelOpCode.INIT_TCP_MULTIPLEXED_REJECTED, new InitRejectedResponse()
                {
                    Reason = "duplicate accessToken. Retry with different AccessToken"
                });
            }
            
            long channelId = ConnectionHandshakeManager.GenerateChannelId();
            
            var channel = new InactiveChannelInfo
            (
                channelId,
                request.AccessToken,
                GenerateToken(),
                DateTime.UtcNow,
                DateTime.UtcNow.AddSeconds(3 * 60),
                DetermineRequiredConnections()
            );
            
            // Move connection from unbound to channel
            if (this.unboundConnections.Remove(connection.ConnectionId) == false)
            {
                return HandshakeResult.Fail("connection is not in unbound connection. cannot proceed.", true);
            }
            
            var boundConn = new BoundConnectionInfo(connection);
            channel.Connections[connection.ConnectionId] = boundConn;
            
            this.inactiveChannels[channelId] = channel;
            this.tokenToChannel[request.AccessToken] = channelId;  // Track token usage
            
            var response = new InitResponse
            {
                ChannelId = channelId,
                ChannelToken = channel.ConnectionToken,
                RequiredConnections = channel.RequiredConnections,
                OptimalConnections = channel.RequiredConnections,
                InitDeadline = channel.ExpiresAt
            };
            
            this.logger.LogInformation("Channel {channelId} created for client {accessToken}", channelId, request.AccessToken);
            
            return HandshakeResult.Success(NetChannelOpCode.INIT_TCP_MULTIPLEXED_RESPONSE, response);
        }
        
        private HandshakeResult ProcessJoin(ReplTcpConnection connection, ReadOnlyMemory<byte> message)
        {
            if (JoinRequest.TryParse(message.Span, out var request) == false)
            {
                return HandshakeResult.Fail("Invalid Join request", true);
            }
            
            // Check if connection already processed handshake
            if (this.TryFindConnection(connection.ConnectionId, out var connectionInfo) == false)
            {
                return HandshakeResult.Fail("Connection does not exist.", true);
            }
            if (connectionInfo.Value.Location != ConnectionLocation.Unbound)
            {
                return HandshakeResult.Fail("Connection not in unbound state", true);
            }
            
            if (connectionInfo.Value.HasProcessedHandshake)
            {
                return HandshakeResult.Fail("Duplicate Join", true);
            }
            
            if (this.inactiveChannels.TryGetValue(request.ChannelId, out var channel) == false)
            {
                return HandshakeResult.Fail("Channel not found", true);
            }
            
            if (!channel.ValidateConnectionToken(request.ChannelToken))
            {
                return HandshakeResult.Fail("Invalid token", true);
            }
            
            if (DateTime.UtcNow > channel.ExpiresAt)
            {
                return HandshakeResult.Fail("Channel expired", true);
            }
            
            this.unboundConnections.Remove(connection.ConnectionId);

            var boundConn = new BoundConnectionInfo(connection);
            channel.Connections[connection.ConnectionId] = boundConn;
            
            var response = new JoinResponse
            {
                Success = true,
                ConnectionIndex = request.ConnectionIndex,
                ActiveConnectionCount = channel.Connections.Count
            };
            
            this.logger.LogInformation("Connection {connectionId} joined channel {channelId}", connection.ConnectionId, request.ChannelId);
            
            if (this.ShouldActivateChannel(channel))
            {
                this.SendChannelReady(channel);
            }
            
            return HandshakeResult.Success(NetChannelOpCode.JOIN_TCP_MULTIPLEXED_RESPONSE, response);
        }
        
        private HandshakeResult ProcessChannelReadyAcknowledge(ReplTcpConnection connection, ReadOnlyMemory<byte> message)
        {
            // var request = AckRequest.Parse(message);
            
            // Find which unbound channel contains this connection
            var channelInfo = this.FindConnectionInUnboundChannels(connection.ConnectionId);
            if (channelInfo == null)
            {
                return HandshakeResult.Fail("Connection not in any unbound channel", true);
            }
            
            var (channel, boundConn) = channelInfo.Value;
            
            if (boundConn.HasAcknowledged)
            {
                return HandshakeResult.Fail("Already acknowledged", true);
            }
            
            boundConn.HasAcknowledged = true;
            channel.AcknowledgedConnections.Add(connection.ConnectionId);

            if (channel.AcknowledgedConnections.Count == channel.Connections.Count)
            {
                this.ActivateChannel(channel);   
            }
            
            return HandshakeResult.Success();
        }
        
        private HandshakeResult ProcessReconnect(ReplTcpConnection connection, ReadOnlyMemory<byte> message)
        {
            if (ReconnectRequest.TryParse(message.Span, out var request) == false)
            {
                return HandshakeResult.Fail("Invalid ReconnectRequest", true);
            }
            
            // Find active channel by session ID
            var session = this.findActiveSession(request.SessionId);
            if (session == null)
            {
                return HandshakeResult.Fail("Session not found", true);
            }

            if (session.NetChannel is not ITcpNetChannel reconnectable)
            {
                return HandshakeResult.Fail("Reconnect is not supported for session's channel", true);
            }
            
            if (reconnectable.HandleReconnection(connection, request.ReconnectToken) == false)
            {
                return HandshakeResult.Fail("failed to add  reconnected connection", true);
            }
            
            this.unboundConnections.Remove(connection.ConnectionId);
            
            var response = new ReconnectResponse
            {
                Success = true,
                ActiveConnectionCount = reconnectable.ConnectionCount,
            };
            
            this.logger.LogInformation("Connection {connectionId} reconnected to session {sessionId}",  connection.ConnectionId, request.SessionId);
            
            return HandshakeResult.Success(NetChannelOpCode.RECONNECT_TCP_MULTIPLEXED_RESPONSE, response);
        }
}