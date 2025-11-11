using System.Diagnostics.CodeAnalysis;
using Repl.Server.Core.MathUtils;
using Repl.Server.Game.Entities.Components;

namespace Repl.Server.Game.Entities; 

public enum EntityType
{
    Player,
    Resource,
}

public abstract class Entity : IDisposable
{
    private readonly Dictionary<Type, IComponent> components = new();
    private bool disposed;

    public int Id { get; }
    public abstract EntityType Type { get; }
    public bool IsMarkedForDeletion { get; private set; }
    public float CreatedAt { get; }

    protected Entity(int id, float currentTime)
    {
        this.Id = id;
        this.CreatedAt = currentTime;
    }
    
    public T AddComponent<T>(T component) where T : class, IComponent
    {
        var type = typeof(T);
        if (this.components.TryAdd(type, component) == false)
        {
            throw new InvalidOperationException($"Entity {Id} already has a component of type {type.Name}.");
        }
        component.OnAttached(this);
        return component;
    }

    public T? GetComponent<T>() where T : class, IComponent
    {
        if (this.components.TryGetValue(typeof(T), out var component) == false)
        {
            return null;
        }
        return component as T;
    }
    
    public bool TryGetComponent<T>([NotNullWhen(true)] out T? component) where T : class, IComponent
    {
        component = this.GetComponent<T>();
        return component != null;
    }

    public bool HasComponent<T>() where T : class, IComponent => this.components.ContainsKey(typeof(T));
    
    public virtual void Update(float deltaTime)
    {
        if (this.IsMarkedForDeletion)
        {
            return;
        }
        
        foreach (var component in this.components.Values)
        {
            if (component.Enabled)
            {
                component.Update(deltaTime);
            }
        }
    }

    public void Destroy() => this.IsMarkedForDeletion = true;
    
    public abstract EntitySnapshot GetEntitySnapshot(float timestamp);

    public void Dispose()
    {
        if (this.disposed)
        {
            return;
        }

        foreach (var component in this.components.Values)
        {
            component.OnDetached();
        }
        this.components.Clear();
        this.disposed = true;
        
        GC.SuppressFinalize(this);
    }
}