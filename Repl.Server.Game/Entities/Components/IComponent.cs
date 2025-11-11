using Repl.Server.Core.MathUtils;

namespace Repl.Server.Game.Entities.Components;

public interface IComponent
{
    Entity Owner { get; }
    
    bool Enabled { get; set; }
    
    void OnAttached(Entity owner);
    
    void OnDetached();
    
    void Update(float deltaTime);
}

public abstract class EntitySnapshot
{
    public long EntityId { get; set; }
    public EntityType EntityType { get; set; }
    public float Timestamp { get; set; }
    public Vector2 Position { get; set; }
    public float Rotation { get; set; }
}

public class PlayerSnapshot : EntitySnapshot
{
    public long ClientId { get; set; }
}

public class ResourceSnapshot : EntitySnapshot
{
    public string ResourceTypeId { get; set; } = "";
    public int StackSize { get; set; }
    public long OwnerClientId { get; set; }
    public bool IsCarried { get; set; }
    public long? CarrierEntityId { get; set; }
}

public class StaticSnapshot : EntitySnapshot
{
    public string StaticTypeId { get; set; } = "";
}

public struct ResourceSpawnData
{
    public Vector2 InitialPosition { get; set; }
    public Vector2 InitialVelocity { get; set; }
    public float Mass { get; set; }
    public float Friction { get; set; }
    public float Restitution { get; set; }
}