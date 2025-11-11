namespace Repl.Server.Core.TaskGraph.ResourceManagement;

public record SharedResource(string Tag, int ResourceId) : IEquatable<SharedResource>
{
    public virtual bool Equals(SharedResource? other)
    {
        if (other is null)
        {
            return false;
        }

        return this.ResourceId == other.ResourceId;
    }
    
    public override int GetHashCode()
    {
        return this.ResourceId.GetHashCode();
    }

    public override string ToString() => $"{this.Tag}({this.ResourceId})";
}
