using System.Net;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Repl.Server.Coordinator.LookupTables;
using ReplInternalProtocol.DataServer;

namespace Repl.Server.Coordinator.StartupExtensions;

public static class CoordinatorServerServiceExtension
{
    private static Uri GetGrpcUri(this IConfiguration configuration, string sectionName)
    {
        var section = configuration.GetSection(sectionName);
        var ip = section["PrivateIp"] ?? IPAddress.Loopback.ToString();
        var port = section["PrivateGrpcPort"];
        if (string.IsNullOrEmpty(port))
        {
            throw new InvalidOperationException($"PrivateGrpcPort not found in {sectionName}");
        }
        return new Uri($"https://{ip}:{port}");
    }

    public static IServiceCollection AddCoordinatorServerServices(this IServiceCollection services)
    {
        services.AddMemoryCache();
        services.AddSingleton<GameServerClientLookupTable>();

        return services;
    }

    public static void AddInternalGrpcClients(this IServiceCollection services)
    {
        services.AddGrpcClient<DataService.DataServiceClient>(options =>
        {
            var config = services.BuildServiceProvider().GetRequiredService<IConfiguration>();
            options.Address = config.GetGrpcUri("DataServiceContext");
        });
    }
}
