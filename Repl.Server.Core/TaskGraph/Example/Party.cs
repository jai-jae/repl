using System.Collections.Concurrent;

namespace Repl.Server.Core.TaskGraph.Example;

public class Party
{
    private readonly List<int> members = new();
    private readonly int capacity;

    public string Name { get; set; } = "Default";

    public IReadOnlyList<int> Members => members;

    public Party(int capacity = 5)
    {
        this.capacity = capacity;
    }

    public bool AddPartyMember(int playerId)
    {
        if (this.members.Count >= this.capacity)
        {
            return false;
        }
        
        this.members.Add(playerId);
        return true;
    }

    public void LeaveParty(int playerId)
    {
        this.members.Remove(playerId);
    }
}

public class PartyManager
{
    private readonly ConcurrentDictionary<long, Party> parties = new();

    public Party GetOrCreate(long partyId, int capacity = 5)
    {
        return this.parties.GetOrAdd(partyId, _ => new Party(capacity));
    }
}