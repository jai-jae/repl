namespace Repl.Server.Core.TaskGraph;

public record Error(string Message);

public readonly struct WorkResult
{
    public Error? Error { get; }

    public bool IsSuccess => Error is null;
    public bool IsFailure => IsSuccess == false;

    private WorkResult(Error? error)
    {
        Error = error;
    }

    public static WorkResult Ok() => new(null);
    public static WorkResult Fail(Error error) => new(error);
}