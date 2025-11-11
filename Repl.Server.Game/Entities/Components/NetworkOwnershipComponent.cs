namespace Repl.Server.Game.Entities.Components;

public enum OwnershipPriority
{
    Default = 0,     // Initial/passive ownership, assigned at spawn.
    Collision = 1,   // Medium priority, granted when a player is physically touching the resource.
    Interaction = 2  // Highest priority, locked when a player is actively carrying the resource.
}

public enum OwnershipResult
{
    Granted,    // Ownership was successfully transferred.
    Denied,     // Ownership was denied based on priority rules.
    AlreadyOwned// The requester is already the owner.
}

public class NetworkOwnershipComponent : IComponent
{
    public Entity Owner { get; private set; } = null!;
    public bool Enabled { get; set; } = true;
    
    public long OwnerClientId { get; private set; }
    public long? PreviousOwnerClientId { get; private set; }
    public long OwnershipChangedTime { get; private set; }
    public OwnershipPriority Priority { get; private set; }

    public NetworkOwnershipComponent(long initialOwnerClientId, long currentTime)
    {
        OwnerClientId = initialOwnerClientId;
        Priority = OwnershipPriority.Default;
        OwnershipChangedTime = currentTime;
    }
    
    public void OnAttached(Entity owner)
    {
        this.Owner = owner;
    }

    public void OnDetached()
    {
        
    }

    public void Update(float deltaTime)
    {
        
    }
    
    public OwnershipResult RequestOwnership(long requestingClientId, OwnershipPriority requestedPriority, long currentTick)
    {
        if (requestedPriority > Priority)
        {
            ChangeOwnership(requestingClientId, requestedPriority, currentTick);
            return OwnershipResult.Granted;
        }
        
        if (requestedPriority == Priority)
        {
            return OwnerClientId == requestingClientId ? OwnershipResult.AlreadyOwned : OwnershipResult.Denied;
        }
        
        return OwnershipResult.Denied;
    }
    
    public void ForceInteractionOwnership(long newOwnerClientId, long currentTick)
    {
        ChangeOwnership(newOwnerClientId, OwnershipPriority.Interaction, currentTick);
    }
    
    public bool ResetPriorityToDefault(long currentTick)
    {
        if (Priority == OwnershipPriority.Default) return false;

        Priority = OwnershipPriority.Default;
        OwnershipChangedTime = currentTick;
        return true;
    }
    
    private void ChangeOwnership(long newOwner, OwnershipPriority newPriority, long currentTick)
    {
        if (OwnerClientId == newOwner && Priority == newPriority) return;

        PreviousOwnerClientId = OwnerClientId;
        OwnerClientId = newOwner;
        Priority = newPriority;
        OwnershipChangedTime = currentTick;
    }
}