using System.Data;
using Microsoft.Extensions.Logging;
using MySqlConnector;
using Repl.Server.Database.Config;

namespace Repl.Server.Database.DatabaseAccesLayers;

public abstract class NonQuery
{
    private readonly ILogger logger;
    private readonly MySqlConnectionFactory connectionFactory;

    protected NonQuery(MySqlConnectionFactory connectionFactory, ILogger logger)
    {
        this.connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public abstract string SqlStatement { get; }
    protected virtual CommandType CommandType => CommandType.Text;
    protected virtual int CommandTimeoutSeconds => 10;

    protected abstract void AddParameters(MySqlCommand command);

    /// <summary>
    /// Executes the non-query and returns the number of affected rows
    /// </summary>
    public async Task<DatabaseResult<int>> ExecuteAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await using var connection = connectionFactory.CreateConnection();
            await connection.OpenAsync(cancellationToken);

            await using var command = new MySqlCommand(SqlStatement, connection);
            command.CommandType = CommandType;
            command.CommandTimeout = CommandTimeoutSeconds;

            await command.PrepareAsync(cancellationToken);
            AddParameters(command);

            var rowsAffected = await command.ExecuteNonQueryAsync(cancellationToken);

            logger.LogDebug("Query {QueryType} affected {RowsAffected} rows", GetType().Name, rowsAffected);

            return DatabaseResult<int>.Success(rowsAffected);
        }
        catch (MySqlException ex)
        {
            logger.LogError(ex, "Database non-query failed: {QueryType}", GetType().Name);
            return DatabaseResult<int>.Failure($"Database operation failed: {ex.Message}", ex);
        }
    }
}