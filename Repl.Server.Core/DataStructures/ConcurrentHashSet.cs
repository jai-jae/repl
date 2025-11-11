using System.Collections;
using System.Collections.Concurrent;

namespace Repl.Server.Core.DataStructures;

public class ConcurrentHashSet<T> : IReadOnlyCollection<T> where T : notnull
{
    private readonly ConcurrentDictionary<T, byte> internalBackend = new();

    public bool Add(T item) => internalBackend.TryAdd(item, 0);

    public bool Remove(T item) => internalBackend.TryRemove(item, out _);

    IEnumerator<T> IEnumerable<T>.GetEnumerator()
    {
        return internalBackend.Keys.GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return internalBackend.GetEnumerator();
    }

    public int Count => internalBackend.Keys.Count;
}
