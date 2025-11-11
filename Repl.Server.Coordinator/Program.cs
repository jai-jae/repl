using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Repl.Server.Coordinator.InternalService;
using Repl.Server.Coordinator.LookupTables;
using Repl.Server.Coordinator.StartupExtensions;
using Repl.Server.Core.Logging;
using Serilog;
using Serilog.Extensions.Logging;
using static ReplInternalProtocol.DataServer.DataService;
using ReplLog = Repl.Server.Core.Logging.Log;


namespace Repl.Server.Coordinator;

public class CoordinatorProgram
{
    public static async Task Main(string[] args)
    {
        // =================================================================================
        // 1. INITIALIZATION LOGGER
        // =================================================================================
        Serilog.Debugging.SelfLog.Enable(Console.Out);
        using ILoggerFactory factory = new SerilogLoggerFactory(SerilogConfigurer.ConfigureAppLogger());
        ReplLog.Initialize(factory);

        var mainLogger = ReplLog.CreateLogger<CoordinatorProgram>();

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
            // 4. KESTREL (WEB HOST) CONFIGURATION
            // =================================================================================
            builder.WebHost.ConfigureKestrel((context, options) =>
            {
                options.AddServerHeader = false;
                var grpcPort = context.Configuration.GetValue<int>("CoordinatorContext:PrivateGrpcPort");
                options.Listen(IPAddress.Any, grpcPort, listenOptions => listenOptions.Protocols = HttpProtocols.Http2);
                mainLogger.LogInformation("Kestrel configured for GameServer on port {port}", grpcPort);
            });

            // =================================================================================
            // 5. SETUP COORDINATOR SERVER
            // =================================================================================
            builder.Services.AddGrpc();
            builder.Services.AddCoordinatorServerServices();
            builder.Services.AddInternalGrpcClients();
            var app = builder.Build();

            // =================================================================================
            // 6. ENDPOINT MAPPING
            // =================================================================================
            var grpcPort = app.Configuration.GetValue<int>("CoordinatorContext:PrivateGrpcPort");
            app.MapGrpcService<ReplCoordinatorService>().RequireHost($"*:{grpcPort}");
            mainLogger.LogInformation("Mapped CoordinatorServer Service to *:{coordinatorServerPort}", grpcPort);

            await app.RunAsync();
        }
        catch (Exception ex)
        {
            mainLogger.LogError(ex, "CoordinatorServer host terminated unexpectedly.");
        }
    }
}