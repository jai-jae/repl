using System.Collections.Concurrent;

namespace Repl.Server.Core.Pooling;

internal interface IBackingContainer<T>
{
    bool TryRent(out T item);
    void Return(T item);
    int Count { get; }
    void Clear();
}

internal sealed class ConcurrentQueueContainer<T> : IBackingContainer<T>
{
    private readonly ConcurrentQueue<T> queue = new();
    private volatile int count;

    public bool TryRent(out T item)
    {
        if (this.queue.TryDequeue(out item!))
        {
            Interlocked.Decrement(ref this.count);
            return true;
        }
        return false;
    }

    public void Return(T item)
    {
        this.queue.Enqueue(item);
        Interlocked.Increment(ref this.count);
    }

    public int Count => this.count;

    public void Clear()
    {
        this.queue.Clear();
        this.count = 0;
    }
}

internal sealed class ConcurrentStackContainer<T> : IBackingContainer<T>
{
    private readonly ConcurrentStack<T> stack = new();
    private volatile int count;

        
    public bool TryRent(out T item)
    {
        if (this.stack.TryPop(out item!))
        {
            Interlocked.Decrement(ref this.count);
            return true;
        }
        return false;
    }

    public void Return(T item)
    {
        this.stack.Push(item);
        Interlocked.Increment(ref this.count);
    }

    public int Count => this.count;

    public void Clear()
    {
        this.stack.Clear();
        this.count = 0;
    }
}

public enum AccessMode
{
    FIFO,
    LIFO
}

public enum LoadingMode
{
    Preload,
    Lazy,
}

public class Pool<T> : IDisposable where T : class
{
    private readonly IBackingContainer<T> container;
    private readonly Func<T> itemFactory;
    private readonly LoadingMode loadingMode;
    private readonly int capacity;
        
    private volatile int rentedCount;  // Currently rented objects
    private volatile bool disposed;

    public Pool(int poolCapacity, Func<T> factory, LoadingMode loadingMode, AccessMode accessMode)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(poolCapacity, 0, nameof(poolCapacity));
        ArgumentNullException.ThrowIfNull(factory, nameof(factory));

        this.capacity = poolCapacity;
        this.itemFactory = factory;
        this.loadingMode = loadingMode;
        this.container = this.CreateBackingContainer(accessMode);

        if (this.loadingMode == LoadingMode.Preload)
        {
            this.PreloadItems();
        }
    }
        
    public int AvailableCount => container.Count;
    public int RentedCount => rentedCount;
    public int Capacity => capacity;
    public bool IsDisposed => disposed;

    private IBackingContainer<T> CreateBackingContainer(AccessMode mode)
    {
        return mode switch
        {
            AccessMode.FIFO => new ConcurrentQueueContainer<T>(),
            _ => new ConcurrentStackContainer<T>()
        };
    }

    private void PreloadItems()
    {
        for (int i = 0; i < this.capacity; i++)
        {
            T item = this.itemFactory.Invoke();
            this.container.Return(item);
        }
    }

    public T Rent()
    {
        ObjectDisposedException.ThrowIf(disposed, nameof(Pool<T>));
            
        T item = loadingMode switch
        {
            LoadingMode.Preload => this.RentEager(),
            LoadingMode.Lazy => this.RentLazy(),
            _ => throw new InvalidOperationException($"Unknown loading mode: {this.loadingMode}")
        };

        Interlocked.Increment(ref this.rentedCount);
        return item;
    }

    private T RentEager()
    {
        if (this.container.TryRent(out T item))
        {
            return item;
        }
        throw new InvalidOperationException("No objects available in eager-loaded pool");
    }

    private T RentLazy()
    {
        if (this.container.TryRent(out T item))
        {
            return item;
        }

        return this.itemFactory.Invoke();
    }
        
    public void Return(T item)
    {
        if (this.disposed)
        {
            // If disposed, dispose the item if it's disposable
            (item as IDisposable)?.Dispose();
            return;
        }

        ArgumentNullException.ThrowIfNull(item, nameof(item));

        // Always return to pool - let the container handle capacity management
        this.container.Return(item);
        Interlocked.Decrement(ref this.rentedCount);
    }
        
    public void Dispose()
    {
        if (this.disposed)
        {
            return;
        }

        this.disposed = true;

        if (typeof(IDisposable).IsAssignableFrom(typeof(T)))
        {
            while (this.container.TryRent(out T item))
            {
                (item as IDisposable)?.Dispose();
            }
        }
            
        this.container.Clear();
    }
}