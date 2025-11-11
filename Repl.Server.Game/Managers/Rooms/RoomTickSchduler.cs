using System.Collections.Concurrent;
using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Repl.Server.Game.Configs;
using Repl.Server.Core.Logging;

namespace Repl.Server.Game.Managers.Rooms;

public enum TickRate
{
    Low = 1,
    High = 2
}

public readonly record struct TickContext(long TickNumber, float DeltaTime);

public interface ITickable
{
    public long Id { get; }
    TickRate RequiredTickRate { get; }
    public bool ShouldTick();
    void Tick(TickContext context);
}

public class RoomTickScheduler : IDisposable
{
    private readonly ILogger<RoomTickScheduler> logger = Log.CreateLogger<RoomTickScheduler>();
    
    private const float HIGH_TICK_RATE = 30.0f;
    private const float LOW_TICK_RATE = 10.0f;
    private static readonly TimeSpan HighTickInterval = TimeSpan.FromSeconds(1.0 / HIGH_TICK_RATE);
    private static readonly TimeSpan LowTickInterval = TimeSpan.FromSeconds(1.0 / LOW_TICK_RATE);
    private readonly RoomTickSchedulerOptions options;
    private readonly ConcurrentDictionary<long, ITickable> highTickRooms = [];
    private readonly ConcurrentDictionary<long, ITickable> lowtTickRooms = [];

    private CancellationTokenSource? cts;
    private Task? task;
    private long highRateTickCount = 0;
    private long lowRateTickCount = 0;

    private bool disposed;
    
    public RoomTickScheduler(IOptions<RoomTickSchedulerOptions> options)
    {
        this.options = options.Value;
        this.Start();
    }

    public void Start()
    {
        if (task is not null)
        {
            return;
        }
        
        this.cts = new CancellationTokenSource();
        this.task = Task.Run(() => LoopAsync(this.cts.Token), this.cts.Token);
    }

    public void RegisterRoom(ITickable room)
    {
        switch (room.RequiredTickRate)
        {
            case TickRate.High:
                this.highTickRooms.TryAdd(room.Id, room);
                break;
            case TickRate.Low:
                this.lowtTickRooms.TryAdd(room.Id, room);
                break;
            default:
                throw new ArgumentException("Invalid tick rate for room.", nameof(room));
        }
    }

    public void UnregisterRoom(long roomId)
    {
        this.highTickRooms.TryRemove(roomId, out _);
        this.lowtTickRooms.TryRemove(roomId, out _);
    }

    private async Task LoopAsync(CancellationToken cancellationToken)
    {
        // The loop runs at the highest frequency needed.
        using (var timer = new PeriodicTimer(HighTickInterval))
        {
            var stopwatch = Stopwatch.StartNew();
            double lastHighTickTimestamp = 0;
            double lastLowTickTimestamp = 0;

            try
            {
                while (await timer.WaitForNextTickAsync(cancellationToken))
                {
                    var currentTimestamp = stopwatch.Elapsed.TotalSeconds;

                    var highRateDeltaTime = (float)(currentTimestamp - lastHighTickTimestamp);
                    lastHighTickTimestamp = currentTimestamp;
                    this.TickHighRateRooms(highRateDeltaTime);
                    
                    if (currentTimestamp - lastLowTickTimestamp >= LowTickInterval.TotalSeconds)
                    {
                        var lowRateDeltaTime = (float)(currentTimestamp - lastLowTickTimestamp);
                        lastLowTickTimestamp = currentTimestamp;
                        this.TickLowRateRooms(lowRateDeltaTime);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // This is expected when the scheduler is stopped.
            }   
        }
    }

    private void TickHighRateRooms(float deltaTime)
    {
        if (this.highTickRooms.IsEmpty)
        {
            return;
        }

        var tickables = this.highTickRooms.Values.Where(r => r.ShouldTick()).ToArray();
        if (tickables.Length == 0)
        {
            return;
        }

        var currentTick = Interlocked.Increment(ref this.highRateTickCount);
        var context = new TickContext(currentTick, deltaTime);

        var loopResult = Parallel.ForEach(
            tickables,
            new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount },
            room => room.Tick(context)
        );
    }

    private void TickLowRateRooms(float deltaTime)
    {
        if (lowtTickRooms.IsEmpty)
        {
            return;
        }

        var tickables = lowtTickRooms.Values.Where(r => r.ShouldTick()).ToArray();
        if (tickables.Length == 0)
        {
            return;
        }

        var currentTick = Interlocked.Increment(ref this.lowRateTickCount);
        var context = new TickContext(currentTick, deltaTime);

        // Your original batching logic preserved
        int batchSize = 8;
        Parallel.For(0, (tickables.Length + batchSize - 1) / batchSize, batchIndex =>
        {
            int start = batchIndex * batchSize;
            int end = Math.Min(start + batchSize, tickables.Length);
            for (int i = start; i < end; i++)
            {
                tickables[i].Tick(context);
            }
        });
    }
    
    public void Dispose()
    {
        this.Dispose(true);
        GC.SuppressFinalize(this);
    }
    
    private void Dispose(bool disposing)
    {
        if (this.disposed == false)
        {
            return;
        }

        if (disposing)
        {
            if (this.cts is null || this.task is null)
            {
                return;
            }

            this.cts.Cancel();
            this.cts.Dispose();
        }

        this.disposed = true;   
    }
}

