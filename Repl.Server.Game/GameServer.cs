using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Net.Sockets;
using Google.Protobuf;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Repl.Server.Core.DataStructures;
using Repl.Server.Core.Logging;
using Repl.Server.Core.Network;
using Repl.Server.Core.Network.NetChannel;
using Repl.Server.Core.Network.Tcp;
using Repl.Server.Core.ReplProtocol;
using Repl.Server.Game.Configs;
using Repl.Server.Game.ConnectionHandshake;
using Repl.Server.Game.ConnectionHandshake.HandshakeInfo;
using Repl.Server.Game.Managers.Rooms;
using Repl.Server.Game.Network;
using static ReplInternalProtocol.Coordinator.CoordinatorService;
using static ReplInternalProtocol.DataServer.DataService;
using INetProtocol = Repl.Server.Core.Network.INetProtocol;


namespace Repl.Server.Game;

public sealed class GameServer : TcpServer<ReplGameSession>
{
    private static long nextClientId = 0;
    private new readonly ILogger<GameServer> logger = Log.CreateLogger<GameServer>();

    private readonly GameServerConfig options;
    private readonly ConnectionHandshakeManager connectionManager;
    private readonly ConcurrentHashSet<ReplGameSession> connectingSessions = [];
    // ConcurrentDictionary<AccountId, ReplGameSession>
    private readonly ConcurrentDictionary<long, ReplGameSession> sessions = []; 

    private readonly RoomManager roomManager;
    private readonly INetProtocol netProtocol;
    private readonly PacketRouter<ReplGameSession> router;
    private readonly object mutex = new object();
    
    private readonly DataServiceClient dataClient;
    private readonly CoordinatorServiceClient coordinatoreClient;
    
    public int MaxUserCount => this.options.MaxUserCount;

    public GameServer(
        INetProtocol netProtocol,
        PacketRouter<ReplGameSession> packetRouter,
        DataServiceClient dataServiceClient,
        CoordinatorServiceClient coordinatorServiceClient,
        RoomManager roomManager,
        IOptions<GameServerConfig> options) : base(options.Value.PublicPort)
    {
        this.options = options.Value;
        this.netProtocol = netProtocol;
        this.roomManager = roomManager;
        this.router = packetRouter;
        this.dataClient = dataServiceClient;
        this.coordinatoreClient = coordinatorServiceClient;
        this.connectionManager = new ConnectionHandshakeManager(
            this.OnChannelActivated,
            (sessionId) =>
            {
                this.sessions.TryGetValue(sessionId, out var session);
                return session;
            });
    }

    protected override ReplTcpConnection CreateConnection(Socket socket)
    {
        var connection = new ReplTcpConnection(socket, 32768); // GameServerConstant.CLIENT_RECV_BUFFER_SIZE);
        return connection;
    }
    
    public bool TryGetSession(long playerId, [NotNullWhen(true)] out ReplGameSession? session)
    {
        return this.sessions.TryGetValue(playerId, out session);
    }
    
    protected override void AddConnection(TcpConnectionBase connection)
    {
        if (connection is ReplTcpConnection replTcpConnection)
        {
            this.connectionManager.OnConnectionEstablished(replTcpConnection);
            replTcpConnection.CompleteProcessPacketEvent += this.connectionManager.OnReceivedHandshakePacket;
            replTcpConnection.ConnectionClosedEvent += this.connectionManager.OnConnectionClosed;
        }
        else
        {
            this.logger.LogWarning($"invalid type of Connection is added.");
            connection.ForceClose();
        }
    }

    public void AddSession(ReplGameSession session)
    {
        this.connectingSessions.Add(session);
        session.Start();
    }

    private void OnSessionAuthenticated(ReplGameSession newSession)
    {
        if (newSession.AccountId == 0)
        {
            logger.LogError($"Session {newSession.ClientId} authenticated without a valid AccountId. Kicking.");
            newSession.Dispose();
            return;
        }

        this.connectingSessions.Remove(newSession);
        
        if (this.sessions.TryGetValue(newSession.AccountId, out var oldSession))
        {
            logger.LogInformation($"Duplicate login for AccountId: {newSession.AccountId}. Kicking old session {oldSession.ClientId}.");
            oldSession.Dispose();
        }
        
        this.sessions[newSession.AccountId] = newSession;
        this.logger.LogDebug($"Session {newSession.ClientId} for Account {newSession.AccountId} is now active.");
    }

    private void OnSessionExited(ReplGameSession session)
    {
        connectingSessions.Remove(session);
        
        // check if exited is activeSession.
        //prevents a late disconnect packet from a kicked session from removing the new, valid session.
        if (session.AccountId != 0 && 
            this.sessions.TryGetValue(session.AccountId, out var activeSession) && 
            activeSession == session)
        {
            this.sessions.Remove(session.AccountId, out _);
            this.logger.LogDebug($"Authenticated session for Account {session.AccountId} removed.");
        }
    }

    private void OnChannelActivated(ActiveChannelInfo channelInfo)
    {
        var channel = new TcpMultiplexedChannel(
            channelInfo.ChannelId,
            channelInfo.ReconnectToken,
            channelInfo.Connections.Values.ToArray());
        var gameSession = new ReplGameSession(Interlocked.Increment(ref nextClientId), channel, this, this.netProtocol, this.dataClient);
        gameSession.SessionEnterServerCompleteEvent += OnSessionAuthenticated;
        gameSession.SessionClosedEvent += OnSessionExited;
        gameSession.SessionPacketReceivedEvent += this.router.OnReceivedGameplayPacket;
        this.AddSession(gameSession);
    }

    public void Clear()
    {
        foreach (var session in sessions.Values)
        {
            session.Dispose();
        }
        sessions.Clear();
    }

    public override Task StopAsync(CancellationToken cancellationToken)
    {
        lock (this.mutex)
        {
            foreach (ReplGameSession session in connectingSessions)
            {
                session.Dispose();
            }

            foreach (ReplGameSession session in sessions.Values)
            {
                session.Dispose();
            }
        }

        return base.StopAsync(cancellationToken);
    }
}
