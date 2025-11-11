using Repl.Server.Game.Messaging;
using Repl.Server.Game.Network;
using Repl.Server.Game.Rooms;
using Repl.Server.Game.Rooms.RoomState;
using static ReplGameProtocol.C2GSProtocol.Types;
using Vector2 = Repl.Server.Core.MathUtils.Vector2;

namespace Repl.Server.Game.MessageHandlers;

[ReplMessageHandler(OpCode.EntityCollisionAction)]
public class CollideEntityActionHandler : GameplayMessageHandler<Packet.Types.EntityCollisionAction>
{
    public override Task HandleAsync(ReplGameSession session, Packet.Types.EntityCollisionAction content)
    {   
        if (session.Room is null)
        {
            session.Dispose();
            return Task.CompletedTask;
        }
        
        if (session.PlayerEntityId.HasValue == false)
        {
            return Task.CompletedTask;
        }


        if (session.Room.TryGetResourceEntity(content.EntityId, out var resource) == false)
        {
            return Task.CompletedTask;    
        }

        var dropVelocity = Vector2.Zero;
        session.Room.QueueInteractionCommand(session.ClientId, session.PlayerEntityId.Value, content.EntityId, InteractionType.Collision);
        return Task.CompletedTask; 
    }
}