namespace Repl.Server.Core.TaskGraph.Tags;

using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;

public sealed class TagFactory
{
    private readonly char _delimiter;
    private readonly ConcurrentDictionary<string, Tag> _cache;
    
    public int Count => _cache.Count;
    
    public TagFactory(char delimiter = ':')
    {
        _delimiter = delimiter;
        _cache = new ConcurrentDictionary<string, Tag>();
    }
    
    public bool TryCreate(string raw, [NotNullWhen(true)] out Tag? tag)
    {
        tag = null;

        if (ValidateRaw(raw) == false)
        {
            return false;
        }

        var segments = raw.Split(_delimiter);

        if (ValidateSegments(segments) == false)
        {
            return false;
        }

        var newTag = new Tag(raw, segments);

        if (_cache.TryAdd(raw, newTag) == true)
        {
            tag = newTag;
            return true;
        }
        
        return false;
    }
    
    public bool TryGet(string raw, [NotNullWhen(true)] out Tag? tag)
    {
        tag = null;

        if (string.IsNullOrEmpty(raw))
        {
            return false;
        }

        return _cache.TryGetValue(raw, out tag);
    }
    
    public bool TryRemove(string raw)
    {
        if (string.IsNullOrEmpty(raw))
        {
            return false;
        }

        return _cache.TryRemove(raw, out _);
    }
    
    public void Clear()
    {
        _cache.Clear();
    }

    private bool ValidateRaw(string raw)
    {
        if (string.IsNullOrEmpty(raw))
        {
            return false;
        }

        // Check for leading or trailing delimiter
        if (raw[0] == _delimiter || raw[raw.Length - 1] == _delimiter)
        {
            return false;
        }

        return true;
    }

    private bool ValidateSegments(string[] segments)
    {
        if (segments.Length == 0)
        {
            return false;
        }

        foreach (var segment in segments)
        {
            if (string.IsNullOrEmpty(segment) == true)
            {
                return false;
            }
        }

        return true;
    }
}
