using Repl.Server.Core.MathUtils;

namespace Repl.Server.Game.Entities.Components;

public class TransformComponent : IComponent
{
    public Entity Owner { get; private set; } = null!;
    public bool Enabled { get; set; } = true;
    public Vector2 Position { get; set; }
    public float Rotation { get; set; }
    public Vector2 LastValidatedPosition { get; set; }
    public float LastUpdateTime { get; set; }
    
    public TransformComponent(Vector2 position, float rotation = 0f)
    {
        Position = position;
        LastValidatedPosition = position;
        Rotation = rotation;
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
    
    public bool UpdateFromClient(Vector2 newPosition, float newRotation, float timestamp)
    {
        var distance = Vector2.Distance(newPosition, Position);
        var deltaTime = timestamp - LastUpdateTime;
        
        if (deltaTime > 0)
        {
            var speed = distance / deltaTime;
            
            if (speed > 50f)
            {
                return false;
            }
        }
        
        Position = newPosition;
        Rotation = newRotation;
        LastValidatedPosition = newPosition;
        LastUpdateTime = timestamp;
        
        return true;
    }
}