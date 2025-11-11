using Repl.Server.Core.MathUtils;
using Repl.Server.Game.Entities;
using Repl.Server.Game.Entities.Components;

namespace Repl.Server.Game.Rooms.RoomState;

public interface IServerEvent
{
    public long Tick { get; set; }
}

public struct CarryStateChangedEvent : IServerEvent
{
    public long Tick { get; set; }
    public int CarrierEntityId { get; set; }
    public int CarryableEntityId { get; set; }
    public bool IsCarried { get; set; } // true for Carry, false for Drop
    public Vector2? DropVelocity { get; set; }
}

public struct OwnershipChangedEvent : IServerEvent
{
    public long Tick { get; set; }
    public long EntityId { get; set; }
    public long NewOwnerClientId { get; set; }
    public OwnershipPriority NewPriority { get; set; }
}

public struct EntityDamagedEvent : IServerEvent
{
    public long Tick { get; set; }
    public long TargetId { get; set; }
    public long AttackerId { get; set; }
    public float DamageDealt { get; set; }
    public float NewHealth { get; set; }
}

public struct EntitySpawnedEvent : IServerEvent
{
    public long Tick { get; set; }
    public long EntityId { get; set; }
    public EntityType EntityType { get; set; }
    public Vector2 Position { get; set; }
    public string? ResourceTypeId { get; set; }
}

public struct EntityDestroyedEvent : IServerEvent
{
    public long Tick { get; set; }
    public long EntityId { get; set; }
}

public struct GameStateUpdate
{
    public long ServerTick { get; set; }
    public List<EntitySnapshot> Snapshots { get; set; }
}
