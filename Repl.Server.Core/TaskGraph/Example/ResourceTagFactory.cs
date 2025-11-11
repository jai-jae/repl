namespace Repl.Server.Core.TaskGraph.Example;


/// TODO: SourceGenerator for TagFactory
/// {ResourceType}.{ResourceId}.{ResourceChildType}
public static class ResourceTagFactory
{
    public static string ForPartyMembers(long partyId) => $"Party:{partyId}:Members";
    public static string ForPartyName(long partyId) => $"Party:{partyId}:Name";
}