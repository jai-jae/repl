using System.Diagnostics;
using System.Net;
using System.Reflection;
using Grpc.Core;
using Grpc.Core.Interceptors;
using Grpc.Net.Client.Configuration;
using Grpc.Net.ClientFactory;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Repl.Server.Core.GrpcInterceptors;
using Repl.Server.Core.Logging;
using Repl.Server.Core.TaskGraph;
using Repl.Server.Core.TaskGraph.Example;
using Repl.Server.Game.InternalService;
using Repl.Server.Game.Managers.Rooms;
using Repl.Server.Game.MessageHandlers;
using Repl.Server.Game.Network;
using Repl.Server.Game.StartupExtension;
using ReplGameProtocol;
using ReplInternalProtocol.Coordinator;
using Serilog;
using Serilog.Extensions.Logging;
using static ReplInternalProtocol.Coordinator.CoordinatorService;
using static ReplInternalProtocol.DataServer.DataService;
using ReplLog = Repl.Server.Core.Logging.Log;
namespace Repl.Server.Game;


internal class GameServerRegistrationService : IHostedService
{
    private readonly ILogger<GameServerRegistrationService> _logger = ReplLog.CreateLogger<GameServerRegistrationService>();
    private readonly CoordinatorServiceClient _coordinatorClient;
    private readonly DataServiceClient _dataServiceClient;

    public GameServerRegistrationService(
        CoordinatorServiceClient coordinatorClient,
        DataServiceClient dataServiceClient)
    {
        _coordinatorClient = coordinatorClient;
        _dataServiceClient = dataServiceClient;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await Task.Delay(2000, cancellationToken);
        await RegisterWithCoordinatorAsync(cancellationToken);
    }

    private async Task RegisterWithCoordinatorAsync(CancellationToken cancellationToken)
    {
        this._logger.LogInformation("Attempting to register GameServer with Coordinator... {address}", _coordinatorClient.ToString());
        try
        {
            await _coordinatorClient.RegisterServerAsync(new RegisterServerRequest
            {
                PrivateIp = "127.0.0.1",
                PublicIp = "127.0.0.1",
                PrivateGrpcPort = 23003,
                PublicGamePort = 23000
            }, cancellationToken: cancellationToken);

            this._logger.LogInformation("GameServer successfully registered with Coordinator.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to register GameServer with Coordinator. The server may not receive players.");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}

public class Program
{
    public static async Task Main(string[] args)
    {
        await PartyExampleRun.Run();
        // =================================================================================
        // 1. SETUP LOGGING
        // =================================================================================
        using ILoggerFactory factory = new SerilogLoggerFactory(SerilogConfigurer.ConfigureAppLogger());
        ReplLog.Initialize(factory);

        var mainLogger = ReplLog.CreateLogger<Program>();

        try
        {
            var builder = WebApplication.CreateBuilder(args);
            builder.Logging.ClearProviders().AddSerilog(SerilogConfigurer.ConfigureLoggerForDI());
            // =================================================================================
            // 2. CONFIGURATION
            // =================================================================================
            builder.Configuration
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("service_config.json", optional: false, reloadOnChange: true)
                .AddEnvironmentVariables()
                .AddCommandLine(args);

            // =================================================================================
            // 3. KESTREL (WEB HOST) CONFIGURATION
            // =================================================================================
            builder.WebHost.ConfigureKestrel((context, options) =>
            {
                options.AddServerHeader = false;
                var grpcPort = context.Configuration.GetValue<int>("GameServerContext:PrivateGrpcPort");
                options.Listen(IPAddress.Any, grpcPort, listenOptions => listenOptions.Protocols = HttpProtocols.Http2);
                mainLogger.LogInformation($"Kestrel configured for GameServer on port {grpcPort}");
            });

            // =================================================================================
            // 4. SETUP GAMESERVER
            // =================================================================================
            mainLogger.LogInformation("Setup GameServer's Services.");
            
            builder.Services.AddGrpc();
            builder.Services.AddGameServerServices();
            builder.Services.AddInternalGrpcClients();

            var app = builder.Build();

            // =================================================================================
            // 5. ENDPOINT MAPPING
            // =================================================================================
            var grpcPort = app.Configuration.GetValue<int>("GameServerContext:PrivateGrpcPort");
            app.MapGrpcService<GameServerService>().RequireHost($"*:{grpcPort}");
            mainLogger.LogInformation($"Mapped GameServerService to *:{grpcPort}");

            await app.RunAsync();
        }
        catch (Exception ex)
        {
            mainLogger.LogError(ex, "GameServer host terminated unexpectedly.");
        }
        finally
        {
            mainLogger.LogInformation("Application shutting down.");
        }
    }
}


