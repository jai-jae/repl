using System.Collections;
using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Net;
using Grpc.Core;
using Grpc.Net.Client;
using Microsoft.Extensions.Logging;
using Repl.Server.Core.Logging;
using ReplInternalProtocol.Common;
using ReplInternalProtocol.GS;
namespace Repl.Server.Coordinator.LookupTables;


public enum NodeStatus
{
    Inactive = 0,
    Active = 1,
}

public class GameServerNode
{
    private readonly ILogger<GameServerNode> logger = Log.CreateLogger<GameServerNode>();

    public NodeStatus Status { get; set; }
    public int Id { get; init; }

    public IPEndPoint GameEndpoint { get; init; }
    public IPEndPoint GrpcEndpoint { get; init; }
    public GameServerGrpc.GameServerGrpcClient Client { get; init; }
    public PeriodicTimer Timer { get; init; }
    public int UserCount { get; set; }
    public int RoomCount { get; set; }


    public event EventHandler<HealthCheckResponse>? HealthCheckEvent;

    public GameServerNode(int id, IPEndPoint gameEndpoint, IPEndPoint grpcEndpoint, GameServerGrpc.GameServerGrpcClient Client, CancellationToken cancel, EventHandler<HealthCheckResponse> onHealthCheckDelegate)
    {
        this.Id = id;
        this.Status = NodeStatus.Inactive;
        this.GameEndpoint = gameEndpoint;
        this.GrpcEndpoint = grpcEndpoint;
        this.Client = Client;
        this.Timer = new PeriodicTimer(GameServerClientLookupTable.MonitorInterval);
        HealthCheckEvent += onHealthCheckDelegate;
        var _ = this.HealthCheckLoop(cancel);
    }

    private async Task HealthCheckLoop(CancellationToken cancel)
    {
        while (await this.Timer.WaitForNextTickAsync(cancel) == true)
        {
            try
            {
                HealthCheckResponse response = await this.Client.HealthCheckAsync(new HealthCheckRequest(), deadline: DateTime.UtcNow.AddSeconds(3),
                    cancellationToken: cancel);
                
                HealthCheckEvent?.Invoke(this, response);

            }
            catch (RpcException ex)
            {
                logger.LogWarning(ex, "HealthCheck failed.");
                HealthCheckEvent?.Invoke(this, new HealthCheckResponse { Status = ServerStatus.Inactive});
            }
        }

        logger.LogError("Stop monitoring Server:{GrpcEndpoint}", this.GrpcEndpoint);
    }

    public void Dispose()
    {
        this.Timer.Dispose();
        HealthCheckEvent = null;
    }
}

public class GameServerClientLookupTable : IEnumerable<(int, GameServerGrpc.GameServerGrpcClient)>
{
    private static readonly ILogger<GameServerClientLookupTable> logger = Log.CreateLogger<GameServerClientLookupTable>();

#if DEBUG
    public static readonly TimeSpan MonitorInterval = TimeSpan.FromSeconds(1);
#else
public static readonly TimeSpan MonitorInterval = TimeSpan.FromSeconds(5);
#endif
    private static int nodeId = 0;
    private static int GenerateNodeId() => Interlocked.Increment(ref nodeId);

    private readonly CancellationTokenSource cts = new CancellationTokenSource();
    private readonly ConcurrentDictionary<int, GameServerNode> nodes = [];

    public int Count => nodes.Values.Count(ch => ch is { Status: NodeStatus.Active });


    public IEnumerable<int> Keys
    {
        get
        {
            foreach (GameServerNode node in nodes.Values.Where(gs => gs is { Status: NodeStatus.Active }))
            {
                yield return node.Id;
            }
        }
    }

    public int FindOrCreateNode(string publicIp, int publicPort, string privateIp, int privateGrpcPort)
    {
        GameServerNode? activeNode = nodes.Values.FirstOrDefault(node =>
            node.GrpcEndpoint.Address.ToString() == privateIp &&
            node.Status == NodeStatus.Inactive);

        if (activeNode is not null)
        {
            return (activeNode.Id);
        }

        return CreateNode(publicIp, publicPort, privateIp, privateGrpcPort);
    }

    public int FirstNode()
    {
        foreach (GameServerNode node in nodes.Values.Where(ch => ch.Status == NodeStatus.Active))
        {
            return node.Id;
        }

        return -1;
    }

    public bool ValidNodeId(int nodeId)
    {
        return nodeId > -1 && this.nodes.ContainsKey(nodeId);
    }

    public bool TryGetClient(int nodeId, [NotNullWhen(true)] out GameServerGrpc.GameServerGrpcClient? client)
    {
        if (!ValidNodeId(nodeId))
        {
            client = null;
            return false;
        }

        client = nodes[nodeId].Client;
        return true;
    }

    public bool TryGetActiveEndpoint(int nodeId, [NotNullWhen(true)] out IPEndPoint? endpoint)
    {
        if (!ValidNodeId(nodeId) || this.nodes.TryGetValue(nodeId, out GameServerNode? node) == false || node.Status is NodeStatus.Inactive)
        {
            endpoint = null;
            return false;
        }

        endpoint = node.GrpcEndpoint;
        return true;
    }

    private int CreateNode(string publicIp, int publicPort, string privateIp, int privatePort)
    {
        IPAddress publicIpAddress = IPAddress.Parse(publicIp);
        IPAddress privateIpAddress = IPAddress.Parse(privateIp);

        int nodeId = GameServerClientLookupTable.GenerateNodeId();
        var gameEndpoint = new IPEndPoint(publicIpAddress, publicPort);

        var grpcUri = new Uri($"http://{privateIpAddress}:{privatePort}");
        var grpcEndpoint = new IPEndPoint(privateIpAddress, privatePort);

        GrpcChannel grpcChannel = GrpcChannel.ForAddress(grpcUri);
        var client = new GameServerGrpc.GameServerGrpcClient(grpcChannel);

        nodes.TryAdd(nodeId, new GameServerNode(nodeId, gameEndpoint, grpcEndpoint, client, this.cts.Token, this.OnHealthcheck));

        return  nodeId;
    }

    private void OnHealthcheck(object? obj, HealthCheckResponse healthCheckResponse)
    {
        if (obj is GameServerNode node)
        {
            ServerStatus status = healthCheckResponse.Status;
            int userCount = healthCheckResponse.ConnectedUserCount;
            int roomCount = healthCheckResponse.RoomCount;

            switch (status)
            {
                case ServerStatus.Active:
                    if (node.Status == NodeStatus.Inactive)
                    {
                        logger.LogInformation("Server {nodeId} has become active", node.Id);
                        this.Active(node, userCount, roomCount);
                    }
                    break;
                default:
                    if (node.Status == NodeStatus.Active)
                    {
                        logger.LogWarning("Server {nodeId} has become inactive due to {Status}", node.Id, status);
                        this.Inactive(node);
                    }
                    break;
            }
        }
    }

    private void Inactive(GameServerNode node)
    {
        node.Status = NodeStatus.Inactive;
    }

    private void Active(GameServerNode node, int userCount, int roomCount)
    {
        node.Status = NodeStatus.Active;
        node.UserCount = userCount;
        node.RoomCount = roomCount;
    }

    public IEnumerator<(int, GameServerGrpc.GameServerGrpcClient)> GetEnumerator()
    {
        foreach (GameServerNode node in this.nodes.Values.Where(ch => ch.Status is NodeStatus.Active))
        {
            yield return (node.Id, node.Client);
        }
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }
}
