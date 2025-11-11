using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Logging;
using Repl.Server.Core.Logging;
using Repl.Server.Core.ReplProtocol;
using Repl.Server.Game.ConnectionHandshake.HandshakeInfo;
using Repl.Server.Game.ConnectionHandshake.Jobs;
using Repl.Server.Game.ConnectionHandshake.Protocol;
using Repl.Server.Game.ConnectionHandshake.States;
using Repl.Server.Game.Network;

namespace Repl.Server.Game.ConnectionHandshake;

public partial class ConnectionHandshakeManager : IDisposable
{
    // ID generation
    private static long nextChannelId = 0;
    private static long GenerateChannelId() => Interlocked.Increment(ref nextChannelId);
    private readonly ILogger<ConnectionHandshakeManager> logger = Log.CreateLogger<ConnectionHandshakeManager>();
        
    // Connection Containers - being in a container defines the state
    private readonly Dictionary<long, UnboundConnectionInfo> unboundConnections;
    // Channel Containers
    private readonly Dictionary<long, InactiveChannelInfo> inactiveChannels;
    // unique client id tracking to prevent multiple initChannel from single client.
    private readonly Dictionary<byte[], long> tokenToChannel;
        
    private readonly ConcurrentQueue<ConnectionHandshakeJob> jobQueue;
    private readonly List<ConnectionHandshakeJob> pendingJobs;
    private volatile int isProcessing;
        
    // Callbacks for session creation
    private readonly Action<ActiveChannelInfo> onChannelActivated;
    private readonly Func<long, ReplGameSession?> findActiveSession;
        
    // Cleanup timer
    private readonly Timer cleanupTimer;
        
    public ConnectionHandshakeManager(
        Action<ActiveChannelInfo> onChannelActivated,
        Func<long, ReplGameSession?> findActiveSession)
    {
        this.onChannelActivated = onChannelActivated;
        this.findActiveSession = findActiveSession;
        this.unboundConnections = new Dictionary<long, UnboundConnectionInfo>();
        this.inactiveChannels = new Dictionary<long, InactiveChannelInfo>();
        this.tokenToChannel = new Dictionary<byte[], long>();
            
        this.jobQueue = new ConcurrentQueue<ConnectionHandshakeJob>();
        this.pendingJobs = new List<ConnectionHandshakeJob>(capacity: 100);
            
        this.cleanupTimer = new Timer(
            ClearExpiredResources,
            null,
            TimeSpan.FromSeconds(1),
            TimeSpan.FromSeconds(1));
    }
        
    public void OnConnectionEstablished(ReplTcpConnection connection)
    {
        this.EnqueueJob(new ConnectionHandshakeJob
        {
            Type = HandshakeManagerJobType.ConnectionEstablished,
            Connection = connection,
            Timestamp = DateTime.UtcNow
        });
    }
        
    public void OnConnectionClosed(long connectionId)
    {
        this.EnqueueJob(new ConnectionHandshakeJob
        {
            Type = HandshakeManagerJobType.ConnectionClosed,
            ConnectionId = connectionId,
            Timestamp = DateTime.UtcNow
        });
    }
        
    public void OnReceivedHandshakePacket(ReplTcpConnection connection, ushort opCode, ReadOnlySpan<byte> message)
    {
        var copied = new ReadOnlyMemory<byte>(message.ToArray());
        this.EnqueueJob(new ConnectionHandshakeJob
        {
            Type = HandshakeManagerJobType.HandshakePacket,
            Connection = connection,
            OpCode = (NetChannelOpCode)opCode,
            Message = copied,
            Timestamp = DateTime.UtcNow
        });
    }
    private void ClearExpiredResources(object? _)
    {
        this.EnqueueJob(new ConnectionHandshakeJob
        {
            Type = HandshakeManagerJobType.CleanupExpired
        });
    }
    
    private void EnqueueJob(ConnectionHandshakeJob job)
    {
        this.jobQueue.Enqueue(job);
        
        if (Interlocked.CompareExchange(ref this.isProcessing, 1, 0) == 0)
        {
            this.BeginProcessing();
            return;
        }
    }
        
    private void BeginProcessing()
    {
        do
        {
            try
            {
                this.pendingJobs.Clear();
                while (this.jobQueue.TryDequeue(out var job))
                {
                    this.pendingJobs.Add(job);
                
                    if (this.pendingJobs.Count > 1000)
                    {
                        this.logger.LogWarning(
                            "Too many connections incoming. PendingJobs still in Queue: {Count}", 
                            this.jobQueue.Count);
                        break;
                    }
                }
            
                foreach (var job in this.pendingJobs)
                {
                    try
                    {
                        this.ProcessJob(job);
                    }
                    catch (Exception ex)
                    {
                        this.logger.LogError(ex, "Error processing job {JobType}", job.Type);
                    
                        job.Connection.ForceClose();
                        CleanupConnection(job.Connection.ConnectionId);
                    }
                }
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, "Unexpected error in processing loop");
            }
            
            Interlocked.Exchange(ref this.isProcessing, 0);
            
            if (this.jobQueue.IsEmpty)
            {
                return;
            }
            
        } while (Interlocked.CompareExchange(ref this.isProcessing, 1, 0) == 0);
    }
        
    private void ProcessJob(ConnectionHandshakeJob job)
    {
        switch (job.Type)
        {
            case HandshakeManagerJobType.ConnectionEstablished:
                this.HandleConnectionEstablished(job.Connection);
                break;
                    
            case HandshakeManagerJobType.ConnectionClosed:
                this.HandleConnectionClosed(job.ConnectionId);
                break;
                    
            case HandshakeManagerJobType.HandshakePacket:
                this.HandleHandshakePacket(job.Connection, job.OpCode, job.Message);
                break;
                
            case HandshakeManagerJobType.CleanupExpired:
                this.HandleCleanupExpired();
                break;
            default:
                throw new ArgumentException($"unknown job type. JobType:{job.Type}");
        }
    }
        
    private void HandleConnectionEstablished(ReplTcpConnection connection)
    {
        var unboundConn = new UnboundConnectionInfo(connection, DateTime.UtcNow, DateTime.UtcNow.AddMilliseconds(1000));
        this.unboundConnections[connection.ConnectionId] = unboundConn;
        this.logger.LogDebug("Connection {connectionId} added to unbound" , connection.ConnectionId);
    }
        
    private void HandleConnectionClosed(long connectionId)
    {
        this.CleanupConnection(connectionId);
        this.logger.LogDebug("Connection {connectionId} closed and cleaned up",  connectionId);
    }
        
    private void HandleHandshakePacket(ReplTcpConnection connection, NetChannelOpCode opCode, ReadOnlyMemory<byte> message)
    {
        // Validate packet is valid for connection state
        if (this.IsPacketValidForState(connection.ConnectionId, opCode) == false)
        {
            this.logger.LogWarning("Invalid packet {opCode} for connection {connectionId} state",  opCode, connection.ConnectionId);
            connection.ForceClose();
            this.CleanupConnection(connection.ConnectionId);
            return;
        }
            
        HandshakeResult result = opCode switch
        {
            NetChannelOpCode.INIT_TCP_MULTIPLEXED_REQUEST => this.ProcessInit(connection, message),
            NetChannelOpCode.JOIN_TCP_MULTIPLEXED_REQUEST => this.ProcessJoin(connection, message),
            NetChannelOpCode.TCP_MULTIPLEXED_CHANNEL_READY_ACKNOWLEDGED => this.ProcessChannelReadyAcknowledge(connection, message),
            NetChannelOpCode.RECONNECT_TCP_MULTIPLEXED_REQUEST => this.ProcessReconnect(connection, message),
            _ => HandshakeResult.Fail("Unknown opcode", true)
        };
            
        if (result.ResponseData is not null)
        {
            this.SendHandshakeResponse(connection, result.ResponseOpCode, result.ResponseData);
        }
            
        if (result.IsSuccess == false && result.ShouldDisconnect)
        {
            connection.ForceClose();
            this.CleanupConnection(connection.ConnectionId);
        }
    }
        
    private void SendHandshakeResponse(ReplTcpConnection connection, NetChannelOpCode opCode, IHandshakeMessage responseData)
    {
        var sendBuffer = HandshakePacketBuilder.CreatePacket((ushort)opCode, responseData);
        connection.Send(sendBuffer);
    }
        
    private bool TryFindConnection(long connectionId, [NotNullWhen(true)] out ConnectionInfo? connectionInfo)
    {
        connectionInfo = null;
            
        if (this.unboundConnections.TryGetValue(connectionId, out var unboundConn))
        {
            connectionInfo = new ConnectionInfo
            {
                Location = ConnectionLocation.Unbound,
                Connection = unboundConn.Connection,
                HasProcessedHandshake = false
            };
            return true;
        }
            
        foreach (var channel in this.inactiveChannels.Values)
        {
            if (channel.Connections.TryGetValue(connectionId, out var boundConn))
            {
                connectionInfo = new ConnectionInfo
                {
                    Location = ConnectionLocation.BoundToChannel,
                    Connection = boundConn.Connection,
                    ChannelId = channel.ChannelId,
                    HasProcessedHandshake = true,
                    HasChannelReadyAcknowledged = boundConn.HasAcknowledged
                };
                return true;
            }
        }
        return false;
    }
        
    private (InactiveChannelInfo channel, BoundConnectionInfo connection)? FindConnectionInUnboundChannels(long connectionId)
    {
        foreach (var channel in this.inactiveChannels.Values)
        {
            if (channel.Connections.TryGetValue(connectionId, out var conn))
            {
                return (channel, conn);
            }
        }
        return null;
    }
        
    private bool IsPacketValidForState(long connectionId, NetChannelOpCode opCode)
    {
        if (this.TryFindConnection(connectionId, out var info) == false)
        {
            return false;
        }
            
        return info.Value.Location switch
        {
            ConnectionLocation.Unbound => 
                opCode == NetChannelOpCode.INIT_TCP_MULTIPLEXED_REQUEST || 
                opCode == NetChannelOpCode.JOIN_TCP_MULTIPLEXED_REQUEST,
                    
            ConnectionLocation.BoundToChannel => 
                opCode == NetChannelOpCode.TCP_MULTIPLEXED_CHANNEL_READY_ACKNOWLEDGED,
                    
            ConnectionLocation.Active => 
                opCode == NetChannelOpCode.RECONNECT_TCP_MULTIPLEXED_REQUEST,
                    
            _ => false
        };
    }
        
    private bool ShouldActivateChannel(InactiveChannelInfo channelInfo)
    {
        return channelInfo.Connections.Count >= channelInfo.RequiredConnections ||
               (DateTime.UtcNow > channelInfo.ExpiresAt.AddMilliseconds(-100) && channelInfo.Connections.Count > 0);
    }
        
    private void SendChannelReady(InactiveChannelInfo channelInfo)
    {
        if (channelInfo.HasSentChannelReady)
        {
            return;
        }
            
        channelInfo.HasSentChannelReady = true;
            
        var readyPacket = new ChannelReadyPacket
        {
            ChannelId = channelInfo.ChannelId,
            FinalConnectionCount = channelInfo.Connections.Count,
            ServerTime = DateTime.UtcNow
        };
            
        foreach (var conn in channelInfo.Connections.Values)
        {
            this.SendHandshakeResponse(conn.Connection, NetChannelOpCode.TCP_MULTIPLEXED_CHANNEL_READY, readyPacket);
            logger.LogInformation("Sent ChannelReady for channel {channelId} to connection: {conn}", channelInfo.ChannelId, conn.Connection.RemoteEndpoint);   
        }
    }
        
    private void ActivateChannel(InactiveChannelInfo inactiveChannelInfo)
    {
        var activeChannel = new ActiveChannelInfo
        {
            ChannelId = inactiveChannelInfo.ChannelId,
            Connections = new Dictionary<long, ReplTcpConnection>(),
            CreatedAt = DateTime.UtcNow,
            ReconnectToken = GenerateToken()
        };
            
        // Transfer connections from bound to active (just the raw connection)
        foreach (var(connectionId, boundConnectionInfo) in inactiveChannelInfo.Connections)
        {
            boundConnectionInfo.Connection.CompleteProcessPacketEvent -= this.OnReceivedHandshakePacket;
            boundConnectionInfo.Connection.ConnectionClosedEvent -= this.OnConnectionClosed;
            activeChannel.Connections[connectionId] = boundConnectionInfo.Connection;
        }
        this.inactiveChannels.Remove(inactiveChannelInfo.ChannelId);
        this.tokenToChannel.Remove(inactiveChannelInfo.AccessToken);
        this.onChannelActivated.Invoke(activeChannel);
        
        var buffer = HandshakePacketBuilder.CreatePacket((ushort)NetChannelOpCode.TCP_MULTIPLEXED_CHANNEL_ACTIVATED, new ChannelActivated
        {
            ChannelId = activeChannel.ChannelId,
        });
        activeChannel.Connections.First().Value.Send(buffer);
            
        this.logger.LogInformation("Channel {channelId} activated", inactiveChannelInfo.ChannelId);
    }
        
    private void CleanupConnection(long connectionId)
    {
        // Remove from unbound connections
        if (this.unboundConnections.Remove(connectionId))
        {
            this.logger.LogDebug("Removed connection {connectionId} from unbound", connectionId);
            return;
        }
            
        foreach (var channel in this.inactiveChannels.Values)
        {
            if (channel.Connections.Remove(connectionId))
            {
                channel.AcknowledgedConnections.Remove(connectionId);
                    
                if (channel.Connections.Count == 0)
                {
                    CleanupChannel(channel.ChannelId);
                }
                    
                this.logger.LogDebug("Removed connection {connectionId} from unbound channel {ChannelId}", connectionId, channel.ChannelId);
                return;
            }
        }
    }
        
    private void CleanupChannel(long channelId)
    {
        if (this.inactiveChannels.TryGetValue(channelId, out var unboundChannel))
        {
            this.inactiveChannels.Remove(channelId);
            this.tokenToChannel.Remove(unboundChannel.AccessToken);

            foreach (var conn in unboundChannel.Connections.Values)
            {
                conn.Connection.ForceClose();
            }
                
            logger.LogInformation("Cleaned up unbound channel {channelId}", channelId);
        }
    }
        
    private void HandleCleanupExpired()
    {
        var now = DateTime.UtcNow;
        var expiredConnectionIds = new List<long>();
        var expiredChannelIds = new List<long>();
            
        foreach (var(connectionId, unboundConnectionInfo) in this.unboundConnections)
        {
            if (unboundConnectionInfo.ExpiresAt < now)
            {
                expiredConnectionIds.Add(connectionId);
            }
        }
            
        foreach (var connId in expiredConnectionIds)
        {
            this.logger.LogDebug("Cleaning up expired unbound connection {connId}", connId);
            if (this.unboundConnections.TryGetValue(connId, out var conn))
            {
                conn.Connection.ForceClose();
            }
            this.CleanupConnection(connId);
        }
            
        foreach (var (channelId, unboundChannelInfo) in this.inactiveChannels)
        {
            if (unboundChannelInfo.ExpiresAt < now)
            {
                expiredChannelIds.Add(channelId);
            }
        }
            
        foreach (var channelId in expiredChannelIds)
        {
            logger.LogDebug("Cleaning up expired unbound channel {channelId}", channelId);
            this.CleanupChannel(channelId);
        }
    }
        
    private int DetermineRequiredConnections()
    {
        // TODO: determine with RTT in minds later.
        return 3;
    }
        
    private byte[] GenerateToken()
    {
        var token = new byte[32];
        using (var rng = System.Security.Cryptography.RandomNumberGenerator.Create())
        {
            rng.GetBytes(token);
        }
        return token;
    }
        
    public void Dispose()
    {
        cleanupTimer.Dispose();
            
        while (!jobQueue.IsEmpty)
        {
            jobQueue.TryDequeue(out var _);
        }
            
        foreach (var connInfo in unboundConnections.Values)
        {
            connInfo.Connection.ForceClose();
        }
    }
}