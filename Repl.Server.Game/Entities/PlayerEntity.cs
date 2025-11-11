using Repl.Server.Core.MathUtils;
using Repl.Server.Game.Entities.Components;

namespace Repl.Server.Game.Entities;

public class PlayerEntity : Entity
{
    public override EntityType Type => EntityType.Player;
    
    /// <summary>
    /// The client ID that controls this player
    /// </summary>
    public long ClientId { get; }
    
    public PlayerEntity(
        int id,
        float currentTime,
        long clientId,
        Vector2 spawnPosition) 
        : base(id, currentTime)
    {
        this.ClientId = clientId;
        this.AddComponent(new TransformComponent(spawnPosition, 0));
        this.AddComponent(new CarrierComponent());
    }
    
    protected void ValidateRequiredComponents()
    {
        if (!HasComponent<TransformComponent>())
        {
            throw new InvalidOperationException($"Player entity {Id} requires {nameof(TransformComponent)}");
        }

        if (!HasComponent<CarrierComponent>())
        {
            throw new InvalidOperationException($"Player entity {Id} requires {nameof(CarrierComponent)}");
        }
    }
    
    public override EntitySnapshot GetEntitySnapshot(float timestamp)
    {
        var transform = this.GetComponent<TransformComponent>()!;
        
        return new PlayerSnapshot
        {
            EntityId = Id,
            EntityType = Type,
            Timestamp = timestamp,
            Position = transform.Position,
            Rotation = transform.Rotation,
            ClientId = ClientId,
        };
    }
}