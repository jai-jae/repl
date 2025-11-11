using System;

namespace Repl.Server.Core.TaskGraph.TaskNode;

public enum TaskResultStatus
{
    Succeeded,
    FailedControlled,
    FailedUncontrolled
}

/// <summary>
/// A wrapper that represents the final state of a TaskNode.
/// Separate expected controlled failures and unexpected exceptions.
/// </summary>
public readonly struct TaskNodeResult
{
    public TaskResultStatus Status { get; }
    public Error? ControlledError { get; }
    public Exception? UncontrolledException { get; }

    private TaskNodeResult(TaskResultStatus status, Error? controlledError, Exception? uncontrolledException)
    {
        Status = status;
        ControlledError = controlledError;
        UncontrolledException = uncontrolledException;
    }

    public static TaskNodeResult Success() => 
        new(TaskResultStatus.Succeeded, null, null);

    public static TaskNodeResult ControlledFail(Error error) => 
        new(TaskResultStatus.FailedControlled, error, null);
        
    public static TaskNodeResult UncontrolledFail(Exception exception) => 
        new(TaskResultStatus.FailedUncontrolled, null, exception);
}