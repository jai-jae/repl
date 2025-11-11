using Google.Protobuf;
using Microsoft.Extensions.Logging;
using Repl.Server.Core.Logging;
using Repl.Server.Core.NetBuffers;
using Repl.Server.Core.Network;
using Repl.Server.Core.Network.NetChannel;
using Repl.Server.Game.Rooms;
using static ReplInternalProtocol.DataServer.DataService;

namespace Repl.Server.Game.Network;

public delegate void SessionPacketReceivedDelegate(ReplGameSession session, ushort opCode, IMessage content);

public sealed partial class ReplGameSession : INetworkSession, IDisposable
{
    public enum SessionState
    {
        Connected,
        Disconnected
    }
    
    private readonly ILogger<ReplGameSession> logger = Log.CreateLogger<ReplGameSession>();

    private int disposed = 0;
    
    private readonly INetProtocol netProtocol;
    private readonly DataServiceClient dataServiceClient;
    private readonly ITcpNetChannel netChannel;
    public event SessionPacketReceivedDelegate? SessionPacketReceivedEvent;
    public event Action<ReplGameSession>? SessionEnterServerCompleteEvent;
    public event Action<ReplGameSession>? SessionClosedEvent;
    
    public SessionState State { get; set; }

    public long ClientId { get; }
    public long AccountId { get; private set; }
    public GameRoom? Room { get; private set; }
    public long? PlayerEntityId { get; private set; }
    
    // Player's Ingame States
    // public ItemManager Item { get; private set; } = null;
    
    public ITcpNetChannel NetChannel => this.netChannel;
    
    public ReplGameSession(long clientId, ITcpNetChannel channel, GameServer server, INetProtocol netProtocol, DataServiceClient dataServiceClient)
    {
        this.ClientId = clientId;
        this.netProtocol = netProtocol;
        this.netChannel = channel;
        this.dataServiceClient = dataServiceClient;
        this.netChannel.ChannelCompleteProcessPacketEvent += OnChannelChannelCompleteProcessPacket;
        this.netChannel.ChannelClosedEvent += OnChannelClosed;
    }

    public bool Start()
    {
        return this.netChannel.Start();
    }

    public void CompleteEnterServer(long accountId)
    {
        this.AccountId = accountId;
        this.logger.LogDebug("Enter Server Complete. Client:{clientId}", this.ClientId);
        this.State = SessionState.Connected;
        SessionEnterServerCompleteEvent?.Invoke(this);
    }
    
    public bool Send<T>(T message) where T : IMessage<T>
    {
        if (this.netProtocol.Serialize(message, out var sendBuffer) == false)
        {
            return false;
        }
        return this.SendBuffer(sendBuffer);
    }

    public bool SendBuffer(SendBuffer sendBuffer) => this.netChannel.Send(sendBuffer);
    public bool SendBuffer(List<SendBuffer> sendBufferList) => this.netChannel.Send(sendBufferList);
    
    public void Dispose(bool disposing)
    {
        if (Interlocked.CompareExchange(ref this.disposed, 1, 0) == 0)
        {
            if (disposing)
            {
                this.SessionClosedEvent?.Invoke(this);
                
                SessionPacketReceivedEvent = null;
                SessionEnterServerCompleteEvent = null;
                SessionClosedEvent = null;
                
                CleanUpGameSessionResources();
                
                this.netChannel.Dispose();
                State = SessionState.Disconnected;
            }
        }
    }

    public void Dispose()
    {
        // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    public string ToLog()
    {
        return "";
    }

    private void CleanUpGameSessionResources()
    {
        this.Room?.PlayerLeave(this.ClientId);
        this.logger.LogInformation($"Clean and save Game related resources!");
    }

    // Event handlers
    private void OnChannelChannelCompleteProcessPacket(ushort opCoode, ReadOnlySpan<byte> body)
    {
        if (netProtocol.Deserialize(opCoode, body, out var message) == false)
        {
            this.logger.LogError("message parse fail. Id:{opCoode}, Size:{length}", opCoode, body.Length);
            this.Dispose();
            return;
        }

        SessionPacketReceivedEvent?.Invoke(this, opCoode, message);
    }

    private void OnChannelClosed()
    {
        //TODO: if logout signal is needed to other server here...
        this.logger.LogDebug($"Network Transport breached.");
        this.Dispose();
    }
}

