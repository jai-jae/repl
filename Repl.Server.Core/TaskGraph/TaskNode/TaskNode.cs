using System.Collections.Concurrent;
using Repl.Server.Core.TaskGraph.ResourceManagement;

namespace Repl.Server.Core.TaskGraph.TaskNode;

public class TaskNode
{
    private readonly Func<CancellationToken, Task<WorkResult>> work;
    private readonly TaskCompletionSource<TaskNodeResult> tcs = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly ConcurrentDictionary<string, TaskNode> predecessors = new();
    private readonly ConcurrentDictionary<string, TaskNode> successors = new();
    
    public string TaskId { get; }
    public int Priority { get; }
    public SortedSet<SharedResource> RequiredResources { get; }
    public Task<TaskNodeResult> CompletionResult => tcs.Task;
    public ICollection<TaskNode> Predecessors => this.predecessors.Values;
    public ICollection<TaskNode> Successors => this.successors.Values;
    
    internal TaskNode(string taskId, IEnumerable<SharedResource> requiredResources, Func<CancellationToken, Task<WorkResult>> work, int priority)
    {
        this.TaskId = taskId;
        this.RequiredResources = new SortedSet<SharedResource>(requiredResources, Comparer<SharedResource>.Default);
        this.work = work;
        this.Priority = priority;
    }

    public TaskNode DependsOn(TaskNode dependentTask)
    {
        if (dependentTask.TaskId == this.TaskId)
        {
            throw new ArgumentException("A task cannot depend on itself.");
        }
        
        if (predecessors.TryAdd(dependentTask.TaskId, dependentTask))
        {
            dependentTask.successors.TryAdd(this.TaskId, this);
        }
        return this;
    }
    
    internal async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        try
        {
            // Resolve all dependencies (direct, indirect)
            if (predecessors.IsEmpty == false)
            {
                var completionResults = await Task.WhenAll(predecessors.Values.Select(d => d.CompletionResult));

                var uncontrolledFailure = completionResults
                    .FirstOrDefault(outcome => outcome.Status == TaskResultStatus.FailedUncontrolled);
                
                if (uncontrolledFailure.UncontrolledException is not null)
                {
                    tcs.TrySetResult(TaskNodeResult.UncontrolledFail(new Exception(
                        "Dependency task failed with an unhandled exception.", uncontrolledFailure.UncontrolledException!)));
                    return; 
                }
            }
            
            cancellationToken.ThrowIfCancellationRequested();
            
            // Execute this.work
            try
            {
                var workResult = await work(cancellationToken);
                if (workResult.IsSuccess)
                {
                    tcs.SetResult(TaskNodeResult.Success());
                }
                else
                {
                    tcs.SetResult(TaskNodeResult.ControlledFail(workResult.Error!));
                }
            }
            // TODO: some exception should crash server. Choose carefully.
            catch (Exception ex) // contain ALL unexpected excpetion from Work
            {
                tcs.SetResult(TaskNodeResult.UncontrolledFail(ex));
            }
        }
        catch (OperationCanceledException)  // handle OperationCanceledException (probably timeout)
        {
            tcs.SetResult(TaskNodeResult.UncontrolledFail(new OperationCanceledException()));
        }
        catch (Exception ex) // Catches unexpected exceptions that may be thrown from the TaskExecutor
        {
            tcs.SetResult(TaskNodeResult.UncontrolledFail(ex));
        }
    }
}