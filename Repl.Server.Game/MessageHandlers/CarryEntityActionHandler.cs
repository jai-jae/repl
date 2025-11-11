using Repl.Server.Game.Managers.Rooms;
using Repl.Server.Game.Messaging;
using Repl.Server.Game.Network;
using Repl.Server.Game.Rooms;
using Repl.Server.Game.Rooms.RoomState;
using static ReplGameProtocol.C2GSProtocol.Types;
using Vector2 = Repl.Server.Core.MathUtils.Vector2;

namespace Repl.Server.Game.MessageHandlers;

[ReplMessageHandler(OpCode.CarryEntityAction)]
public class CarryEntityActionHandler : GameplayMessageHandler<Packet.Types.CarryEntityAction>
{
    public override Task HandleAsync(ReplGameSession session, Packet.Types.CarryEntityAction content)
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
        
        session.Room.QueueInteractionCommand(session.ClientId, session.PlayerEntityId.Value, content.EntityId, InteractionType.PickupAttempt);
        return Task.CompletedTask; 
    }
}