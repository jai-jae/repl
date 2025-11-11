using Repl.Server.Core.MathUtils;

namespace Repl.Server.Game.Rooms.RoomState;

public struct ClientTransformMessage
{
    public long ClientId { get; set; }
    public long EntityId { get; set; }
    public Vector2 Position { get; set; }
    public Vector2 Velocity { get; set; }
    public float Rotation { get; set; }
    public float Timestamp { get; set; }
}

public struct ClientInteractionMessage
{
    public long ClientId { get; set; }
    public long PlayerEntityId { get; set; }
    public InteractionType Type { get; set; }
    public long TargetEntityId { get; set; }
    public Vector2? DropVelocity { get; set; } // For drop interactions
    public float Timestamp { get; set; }
}