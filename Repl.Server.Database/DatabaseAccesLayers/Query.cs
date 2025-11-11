using System.Data;
using Microsoft.Extensions.Logging;
using MySqlConnector;
using Repl.Server.Database.Config;

namespace Repl.Server.Database.DatabaseAccesLayers;

public abstract class Query<T>
{
    private readonly ILogger logger;
    private readonly MySqlConnectionFactory connectionFactory;

    protected Query(MySqlConnectionFactory connectionFactory, ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(connectionFactory, nameof(connectionFactory));

        this.connectionFactory = connectionFactory;
        this.logger = logger;
    }

    public abstract string SqlStatement { get; }
    protected CommandType CommandType => CommandType.Text;
    protected int CommandTimeoutSeconds => 10;
    protected abstract void AddParameters(MySqlCommand command);
    protected abstract T? MapResult(MySqlDataReader reader);

    public async Task<DatabaseResult<T>> ExecuteAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            return await ExecuteInternalAsync(cancellationToken);
        }
        catch (MySqlException ex)
        {
            logger.LogError(ex, "Database query failed: {QueryType}", GetType().Name);
            return DatabaseResult<T>.Failure($"Database query failed: {ex.Message}", ex);
        }
    }

    private async Task<DatabaseResult<T>> ExecuteInternalAsync(CancellationToken cancellationToken)
    {
        await using var connection = connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        await using var command = new MySqlCommand(SqlStatement, connection);
        command.CommandType = CommandType;
        command.CommandTimeout = CommandTimeoutSeconds;

        await command.PrepareAsync(cancellationToken);
        this.AddParameters(command);

        this.PrintQueryLog(command);

        await using var reader = await command.ExecuteReaderAsync(CommandBehavior.SequentialAccess, cancellationToken);

        if (!reader.HasRows)
        {
            return DatabaseResult<T>.NotFound();
        }

        var result = this.MapResult(reader);
        return result is not null
            ? DatabaseResult<T>.Success(result)
            : DatabaseResult<T>.NotFound();
    }

    private void PrintQueryLog(MySqlCommand command)
    {
        if (command.Parameters.Count > 0)
        {
            var parameters = string.Join(", ",
                command.Parameters.Cast<MySqlParameter>()
                    .Select(p => $"{p.ParameterName}={p.Value}"));

            logger.LogDebug("Executing {QueryType} with parameters: {Parameters}", GetType().Name, parameters);
        }
        else
        {
            logger.LogError("Executing {QueryType} with  no parameters", GetType().Name);
        }
    }
}