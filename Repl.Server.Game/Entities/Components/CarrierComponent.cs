using System.Diagnostics.CodeAnalysis;


namespace Repl.Server.Game.Entities.Components;

public class CarrierComponent : IComponent
{
    public Entity Owner { get; private set; } = null!;
    public bool Enabled { get; set; } = true;
        
    private readonly Stack<KeyValuePair<int, float>> carriedItems = new(); // Key: EntityId, Value: Weight
    
    public float MaxCarryCapacity { get; set; } = 10f;
    
    public float CurrentCarryWeight { get; private set; }
    
    public float PickupRange { get; set; } = 2.5f; // margin for leniency
    
    public bool IsCarrying => carriedItems.Count > 0;

    public CarrierComponent(float pickupRange = 2f, float maxCapacity = 10f)
    {
        PickupRange = pickupRange;
        MaxCarryCapacity = maxCapacity;
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
        return;
    }

    internal void AssignCarriedItem(int entityId, float weight)
    {
        carriedItems.Push(new KeyValuePair<int, float>(entityId, weight));
        CurrentCarryWeight += weight;
    }
    
    internal bool TryClearCarriedItem([NotNullWhen(true)] out int? entityId, [NotNullWhen(true)] out float? weight)
    {
        if (carriedItems.TryPop(out var item))
        {
            entityId = item.Key;
            weight = item.Value;
            this.CurrentCarryWeight -= item.Value;
            return true;
        }

        entityId = null;
        weight = null;
        return false;
    }
    
    internal List<KeyValuePair<int, float>> ForceClearAllCarriedItems()
    {
        var droppedItems = new List<KeyValuePair<int, float>>(carriedItems);
        this.carriedItems.Clear();
        this.CurrentCarryWeight = 0;
        return droppedItems;
    }
        
    public float GetWeightPenaltyMultiplier()
    {
        if (MaxCarryCapacity <= 0)
        {
            return 1f;
        }
        var weightRatio = CurrentCarryWeight / MaxCarryCapacity;
        return Math.Clamp(1.0f - weightRatio, 0.0f, 1.0f);
    }
}