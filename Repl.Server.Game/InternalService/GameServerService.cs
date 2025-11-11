using Grpc.Core;
using Microsoft.Extensions.Logging;
using ReplInternalProtocol.Common;
using ReplInternalProtocol.GS;
using Void = ReplInternalProtocol.Common.Void;

namespace Repl.Server.Game.InternalService;

public class GameServerService : GameServerGrpc.GameServerGrpcBase
{
    private readonly ILogger<GameServerService> _logger;
    public GameServerService(ILogger<GameServerService> logger)
    {
        this._logger = logger;
    }

    public override Task<HealthCheckResponse> HealthCheck(HealthCheckRequest request, ServerCallContext context)
    {
        this._logger.LogDebug("healthcheck!");
        return Task.FromResult(new HealthCheckResponse
        {
            Status = ServerStatus.Active,
            ConnectedUserCount = 0
        });
    }
}