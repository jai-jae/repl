using Grpc.Core;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Repl.Server.Coordinator.LookupTables;
using ReplInternalProtocol.Coordinator;

namespace Repl.Server.Coordinator.InternalService;

public partial class ReplCoordinatorService : CoordinatorService.CoordinatorServiceBase
{
    private readonly ILogger<ReplCoordinatorService> logger;
    private readonly GameServerClientLookupTable gsClientLookupTable;
    private readonly IMemoryCache tokenCache;


    public ReplCoordinatorService(GameServerClientLookupTable gsClientLookup, IMemoryCache cache, ILogger<ReplCoordinatorService> logger)
    {
        this.gsClientLookupTable = gsClientLookup;
        this.tokenCache = cache;
        this.logger = logger;
    }
        
    public override Task<RegisterServerResponse> RegisterServer(RegisterServerRequest request, ServerCallContext context)
    {
        int nodeId = gsClientLookupTable.FindOrCreateNode(request.PublicIp, request.PublicGamePort, request.PrivateIp, request.PrivateGrpcPort);

        return Task.FromResult(new RegisterServerResponse
        {
            NodeId = nodeId
        });
    }
}