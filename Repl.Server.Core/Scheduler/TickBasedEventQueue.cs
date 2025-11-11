using System.Collections.Concurrent;

namespace Repl.Server.Core.Scheduler;

public class TickScheduler
{
    private readonly ConcurrentQueue<TimedEvent> pendingTasks = new();
    private readonly List<TimedEvent> activeTasks = [];
    
    public long CurrentTick { get; private set; }
    public int TickRate { get; }
    
    public TickScheduler(int tickRate)
    {
        if (tickRate <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(tickRate), "Tick rate must be positive.");
        }
        TickRate = tickRate;
        CurrentTick = 0;
    }
    
    public void Schedule(Action task)
    {
        // Execute on the next tick to ensure consistent behavior.
        pendingTasks.Enqueue(new TimedEvent(task, CurrentTick + 1));
    }
    
    public void ScheduleInTicks(Action task, int delayInTicks)
    {
        long executionTick = CurrentTick + delayInTicks;
        pendingTasks.Enqueue(new TimedEvent(task, executionTick));
    }
    
    public void Schedule(Action task, TimeSpan delay)
    {
        int delayInTicks = TimeSpanToTicks(delay);
        ScheduleInTicks(task, delayInTicks);
    }
    
    public void ScheduleRepeatedInTicks(Action task, int intervalInTicks, int? initialDelayInTicks = null, bool isStrict = false)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(intervalInTicks, 0, nameof(intervalInTicks));
        
        // If initial delay isn't specified, default it to the interval (equivalent to the old 'skipFirst=true').
        long executionTick = CurrentTick + (initialDelayInTicks ?? intervalInTicks);
        pendingTasks.Enqueue(new TimedEvent(task, executionTick, intervalInTicks, isStrict));
    }
    
    public void ScheduleRepeated(Action task, TimeSpan interval, TimeSpan? initialDelay = null, bool isStrict = false)
    {
        int intervalInTicks = TimeSpanToTicks(interval);
        int? initialDelayInTicks = initialDelay.HasValue ? TimeSpanToTicks(initialDelay.Value) : null;
        ScheduleRepeatedInTicks(task, intervalInTicks, initialDelayInTicks, isStrict);
    }
    
    public void Tick()
    {
        CurrentTick++;
        
        while (pendingTasks.TryDequeue(out var newTask))
        {
            activeTasks.Add(newTask);
        }
        
        for (int i = activeTasks.Count - 1; i >= 0; i--)
        {
            var task = activeTasks[i];
            if (task.IsReady(CurrentTick))
            {
                bool shouldRepeat = task.Invoke(CurrentTick);
                if (!shouldRepeat)
                {
                    activeTasks.RemoveAt(i);
                }
            }
        }
    }
    
    private int TimeSpanToTicks(TimeSpan time)
    {
        if (time <= TimeSpan.Zero) return 0;
        return (int)Math.Ceiling(time.TotalSeconds * TickRate);
    }
}