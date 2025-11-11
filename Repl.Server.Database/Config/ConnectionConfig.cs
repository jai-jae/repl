using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MySqlConnector;

namespace Repl.Server.Database.Config;

public sealed record DatabaseConfiguration
{
    public required string Server { get; init; }
    public required string UserId { get; init; }
    public required string Password { get; init; }
    public required string Database { get; init; }
    public uint Port { get; init; } = 3306;
    public bool Pooling { get; init; } = true;
    public uint MinimumPoolSize { get; init; } = 5;
    public uint MaximumPoolSize { get; init; } = 100;
    public uint ConnectionTimeout { get; init; } = 30;
    public uint CommandTimeout { get; init; } = 30;
}

public sealed class MySqlConnectionFactory
{
    private readonly string connectionString;
    private readonly ILogger<MySqlConnectionFactory> logger;

    public MySqlConnectionFactory(IOptions<DatabaseConfiguration> config, ILogger<MySqlConnectionFactory> logger)
    {
        ArgumentNullException.ThrowIfNull(config?.Value, nameof(config));

        this.logger = logger;
        this.connectionString = BuildConnectionString(config.Value);
    }

    private static string BuildConnectionString(DatabaseConfiguration config)
    {
        var builder = new MySqlConnectionStringBuilder
        {
            Server = config.Server,
            UserID = config.UserId,
            Password = config.Password,
            Database = config.Database,
            Port = config.Port,
            Pooling = config.Pooling,
            MinimumPoolSize = config.MinimumPoolSize,
            MaximumPoolSize = config.MaximumPoolSize,
            ConnectionTimeout = config.ConnectionTimeout,
            DefaultCommandTimeout = config.CommandTimeout,

            SslMode = MySqlSslMode.Required,
            AllowUserVariables = false,
            AllowZeroDateTime = false,
            ConvertZeroDateTime = true,
            UseAffectedRows = true,
                
            ConnectionReset = true,
            ConnectionLifeTime = 0,
        };

        return builder.ConnectionString;
    }

    public MySqlConnection CreateConnection() => new(this.connectionString);
}