namespace Repl.Server.Core.TaskGraph.Tags;

public static class TagRelation
{
    public static bool IsAncestorOf(Tag? a, Tag? b)
    {
        if (a is null || b is null)
        {
            return false;
        }

        // Ancestor must have fewer segments
        if (b.Depth <= a.Depth)
        {
            return false;
        }

        // All segments of A must match the beginning of B
        for (int i = 0; i < a.Depth; i++)
        {
            if (a.Segments[i] != b.Segments[i])
            {
                return false;
            }
        }

        return true;
    }
    
    public static bool IsDescendantOf(Tag? a, Tag? b)
    {
        return IsAncestorOf(b, a);
    }
    
    public static bool IsSameAs(Tag? a, Tag? b)
    {
        if (a is null || b is null)
        {
            return false;
        }

        return a.Equals(b);
    }
    
    public static bool AreRelated(Tag? a, Tag? b)
    {
        if (a is null || b is null)
        {
            return false;
        }

        return IsSameAs(a, b) || IsAncestorOf(a, b) || IsDescendantOf(a, b);
    }
    
    public static bool AreRelated(Tag[]? tagsA, Tag[]? tagsB)
    {
        if (tagsA is null || tagsB is null)
        {
            return false;
        }

        if (tagsA.Length == 0 || tagsB.Length == 0)
        {
            return false;
        }

        foreach (var a in tagsA)
        {
            foreach (var b in tagsB)
            {
                if (AreRelated(a, b) == true)
                {
                    return true;
                }
            }
        }

        return false;
    }
}