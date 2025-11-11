using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Repl.Server.Database.Config;

namespace Repl.Server.Database.StartupExtensions;

public static class DataServerServiceExtension
{
    public static IServiceCollection AddDataServerService(this IServiceCollection services)
    {
        var config = services.BuildServiceProvider().GetRequiredService<IConfiguration>();
        services.Configure<DatabaseConfiguration>(config.GetSection("DataServerContext:Database"));
        services.AddSingleton<MySqlConnectionFactory>();

        return services;
    }
}