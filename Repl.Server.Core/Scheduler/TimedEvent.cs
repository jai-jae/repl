namespace Repl.Server.Core.Scheduler;

internal class TimedEvent
{
    public long ExecutionTick { get; private set; }
    private readonly Action task;
    private readonly long intervalInTicks;
    private readonly bool isStrict;

    public TimedEvent(Action task, long executionTick, long intervalInTicks = -1, bool isStrict = false)
    {
        this.task = task ?? throw new ArgumentNullException(nameof(task));
        ExecutionTick = executionTick;
        this.intervalInTicks = intervalInTicks;
        this.isStrict = isStrict;
    }

    public bool IsReady(long currentTick) => ExecutionTick <= currentTick;
    
    public bool Invoke(long currentTick)
    {
        task.Invoke();

        // If the interval is zero or negative, it's a fire-forget task.
        if (intervalInTicks <= 0)
        {
            return false; // Signal to remove the task.
        }

        // Reschedule for the next execution.
        if (isStrict)
        {
            // Strict timing: The next execution is based on the original scheduled time.
            // This maintains a consistent cadence even if a tick is delayed.
            ExecutionTick += intervalInTicks;
        }
        else
        {
            // Non-strict timing: The next execution is based on the current time.
            // This is better for events that should happen 'x' time after the last execution.
            ExecutionTick = currentTick + intervalInTicks;
        }

        return true; // Signal to keep the task.
    }
}