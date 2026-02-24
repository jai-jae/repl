namespace Repl.Server.Core.TaskGraph.Tags;

using System;
using System.Linq;


public sealed class Tag : IEquatable<Tag>
{
    public string Raw { get; }
    
    public string[] Segments { get; }
    
    public int Depth => Segments.Length;
    
    internal Tag(string raw, string[] segments)
    {
        Raw = raw;
        Segments = segments;
    }

    public bool Equals(Tag? other)
    {
        if (other is null)
        {
            return false;
        }

        if (ReferenceEquals(this, other) == true)
        {
            return true;
        }
        
        return Segments.SequenceEqual(other.Segments);
    }

    public override bool Equals(object? obj)
    {
        return obj is Tag other && Equals(other);
    }

    public override int GetHashCode()
    {
        unchecked
        {
            int hash = 17;
            foreach (var segment in Segments)
            {
                hash = hash * 31 + (segment?.GetHashCode() ?? 0);
            }
            return hash;
        }
    }

    public static bool operator ==(Tag? left, Tag? right)
    {
        if (left is null)
        {
            return right is null;
        }
        return left.Equals(right);
    }

    public static bool operator !=(Tag? left, Tag? right)
    {
        return !(left == right);
    }

    public override string ToString()
    {
        return Raw;
    }
}
