using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Repl.Server.Core.Logging;
using Repl.Server.Database.Config;
using Repl.Server.Database.Service;
using Repl.Server.Database.StartupExtensions;
using Serilog;
using Serilog.Extensions.Logging;
using ReplLog = Repl.Server.Core.Logging.Log;

namespace Repl.Server.Database;

public class DataServerProgram
{
    public static async Task Main(string[] args)
    {
        // =================================================================================
        // 1. INITIALIZATION LOGGER
        // =================================================================================
        Serilog.Debugging.SelfLog.Enable(Console.Out);
        using ILoggerFactory factory = new SerilogLoggerFactory(SerilogConfigurer.ConfigureAppLogger());
        ReplLog.Initialize(factory);

        var mainLogger = ReplLog.CreateLogger<DataServerProgram>();

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
                var grpcPort = context.Configuration.GetValue<int>("DataServiceContext:PrivateGrpcPort");
                options.Listen(IPAddress.Any, grpcPort, listenOptions => listenOptions.Protocols = HttpProtocols.Http2);
                mainLogger.LogInformation("Kestrel configured for DataServer on port {dataServiceGrpcPort}", grpcPort);
            });

            // =================================================================================
            // 5. SETUP COORDINATOR SERVER
            // =================================================================================
            builder.Services.AddGrpc();
            builder.Services.AddDataServerService();
            var app = builder.Build();

            // =================================================================================
            // 6. ENDPOINT MAPPING
            // =================================================================================
            var grpcPort = app.Configuration.GetValue<int>("dataServiceGrpcPort:PrivateGrpcPort");
            app.MapGrpcService<ReplDataService>().RequireHost($"*:{grpcPort}");
            mainLogger.LogInformation("Mapped DataServer Service to *:{grpcPort}", grpcPort);

            await app.RunAsync();
        }
        catch (Exception ex)
        {
            mainLogger.LogError(ex, "DataServer host terminated unexpectedly.");
        }
    }
}