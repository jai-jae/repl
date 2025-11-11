namespace Repl.Server.Database.DatabaseAccesLayers;

public enum QueryResultStatus
{
    Success,
    NotFound,
    Error,
}

public class DatabaseResult<T>
{
    public T? Value { get; }
    public QueryResultStatus Status { get; }
    public string? ErrorMessage { get; }
    public Exception? Exception { get; }

    private DatabaseResult(T? value, QueryResultStatus status, string? errorMessage, Exception? exception)
    {
        Value = value;
        Status = status;
        ErrorMessage = errorMessage;
        Exception = exception;
    }

    public static DatabaseResult<T> Success(T value)
    {
        ArgumentNullException.ThrowIfNull(nameof(value));
        return new(value, QueryResultStatus.Success, null, null);
    }

    public static DatabaseResult<T> NotFound() =>
        new(default, QueryResultStatus.NotFound, "No records found", null);

    public static DatabaseResult<T> Failure(string message, Exception? exception = null) =>
        new(default, QueryResultStatus.Error, message, exception);
}