using Repl.Server.Core.MathUtils;
namespace Repl.Server.Game.Entities.Components;

public class CarryableComponent : IComponent
{
    public Entity Owner { get; private set; } = null!;
    public bool Enabled { get; set; } = true;
    public bool IsBeingCarried { get; private set; }
    public int? CarrierEntityId { get; private set; }
    public float Weight { get; set; } = 1f;
    public Vector2? LastDropVelocity { get; private set; }
        
    public CarryableComponent(float weight = 1f)
    {
        Weight = weight;
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
    
    internal void PickUp(int carrierId)
    {
        if (IsBeingCarried) return;

        IsBeingCarried = true;
        CarrierEntityId = carrierId;
        LastDropVelocity = null;
    }

    internal void Drop(Vector2? dropVelocity)
    {
        if (!IsBeingCarried) return;

        IsBeingCarried = false;
        CarrierEntityId = null;
        LastDropVelocity = dropVelocity;
    }
}