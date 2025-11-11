using Repl.Server.Core.MathUtils;
using Repl.Server.Game.Entities.Components;

namespace Repl.Server.Game.Entities;

public class ResourceEntity : Entity
{
    public override EntityType Type => EntityType.Resource;
    public string ResourceTypeId { get; }
    public int StackSize { get; set; } = 1;
    
    public ResourceSpawnData SpawnData { get; }
    
    public ResourceEntity(
        int id,
        long initialOwnerClientId,
        long currentTick,
        string resourceTypeId,
        Vector2 spawnPosition,
        ResourceSpawnData spawnData)
        : base(id, currentTick)
    {
        this.ResourceTypeId = resourceTypeId;
        this.SpawnData = spawnData;
        this.AddComponent(new TransformComponent(Vector2.Zero, 0));
        this.AddComponent(new NetworkOwnershipComponent(initialOwnerClientId, currentTick));
        this.AddComponent(new CarryableComponent());
    }
    
    protected void ValidateRequiredComponents()
    {
        if (!HasComponent<TransformComponent>())
            throw new InvalidOperationException($"Resource entity {Id} requires TransformComponent");
        if (!HasComponent<NetworkOwnershipComponent>())
            throw new InvalidOperationException($"Resource entity {Id} requires NetworkOwnershipComponent");
        if (!HasComponent<CarryableComponent>())
            throw new InvalidOperationException($"Resource entity {Id} requires CarryableComponent");
    }
    
    public override EntitySnapshot GetEntitySnapshot(float timestamp)
    {
        var transform = GetComponent<TransformComponent>()!;
        var ownership = GetComponent<NetworkOwnershipComponent>()!;
        var carryable = GetComponent<CarryableComponent>()!;
        
        return new ResourceSnapshot
        {
            EntityId = Id,
            EntityType = Type,
            Timestamp = timestamp,
            Position = transform.Position,
            Rotation = transform.Rotation,
            ResourceTypeId = ResourceTypeId,
            StackSize = StackSize,
            OwnerClientId = ownership.OwnerClientId,
            IsCarried = carryable.IsBeingCarried,
            CarrierEntityId = carryable.CarrierEntityId
        };
    }
}