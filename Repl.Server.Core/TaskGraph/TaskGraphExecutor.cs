namespace Repl.Server.Core.TaskGraph;

public class TaskGraphExecutor
{
    private readonly int maxConcurrency;
    private readonly SemaphoreSlim concurrencySemaphore;
    
    public TaskGraphExecutor(int maxConcurrency)
    {
        var concurrency = Math.Max(Math.Min(maxConcurrency, Environment.ProcessorCount), 1);
        concurrencySemaphore = new SemaphoreSlim(concurrency, concurrency);
    }

    public async Task ExecuteAsync(TaskGraph graphBuilder, CancellationToken cancellationToken = default)
    {
        var executionTasks = new List<Task>();
        
        foreach (var node in graphBuilder.Tasks)
        {
            executionTasks.Add(this.ExecuteTaskNodeOnSemaphoreAsync(node, cancellationToken));
        }
        
        // Any exception threw in task is always contained and propagated accordingly to dependents.
        await Task.WhenAll(executionTasks);
    }

    private async Task ExecuteTaskNodeOnSemaphoreAsync(TaskNode.TaskNode node, CancellationToken cancellationToken)
    {
        await this.concurrencySemaphore.WaitAsync(cancellationToken);
        
        try
        {
            await node.ExecuteAsync(cancellationToken);
        }
        finally
        {
            this.concurrencySemaphore.Release();
        }
    }
}