using System.Collections.Concurrent;

namespace Repl.Server.Core.TaskGraph.ResourceManagement;

public class SharedResourceManager
{
    private readonly ConcurrentDictionary<string, SharedResource> resources = new();
    private int nextResourceId = 0;
    private int GenerateResourceId() => Interlocked.Increment(ref this.nextResourceId);

    public SharedResource GetOrCreateResource(string resourceTag)
    {
        return resources.GetOrAdd(resourceTag, tag =>
        {
            var resourceId = this.GenerateResourceId();
            return new SharedResource(tag, resourceId);
        });
    }
}