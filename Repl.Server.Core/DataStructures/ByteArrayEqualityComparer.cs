namespace Repl.Server.Core.DataStructures;

public sealed class ByteArrayEqualityComparer : IEqualityComparer<byte[]>
{
    public static readonly ByteArrayEqualityComparer Instance = new();

    public bool Equals(byte[]? x, byte[]? y)
    {
        if (ReferenceEquals(x, y))
        {
            return true;
        }

        if (x is null || y is null)
        {
            return false;
        }
        
        return x.AsSpan().SequenceEqual(y);
    }

    public int GetHashCode(byte[] obj)
    {
        var hash = new HashCode();
        foreach (var b in obj)
        {
            hash.Add(b);   
        }
        return hash.ToHashCode();
    }
}
