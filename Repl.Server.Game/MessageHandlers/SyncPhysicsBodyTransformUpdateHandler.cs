using Repl.Server.Game.Messaging;
using Repl.Server.Game.Network;
using static ReplGameProtocol.C2GSProtocol.Types;
using Vector2 = Repl.Server.Core.MathUtils.Vector2;

namespace Repl.Server.Game.MessageHandlers;


[ReplMessageHandler(OpCode.SyncPhysicsBodyTransformUpdate)]
public class SyncPhysicsBodyTransformUpdateHandler : GameplayMessageHandler<Packet.Types.SyncPhysicsBodyTransformUpdate>
{
    public override Task HandleAsync(ReplGameSession session, Packet.Types.SyncPhysicsBodyTransformUpdate content)
    {
        if (session.Room is null)
        {
            session.Dispose();
            return Task.CompletedTask;
        }
        
        if (session.Room.ValidateClientOwnsEntity(session.ClientId, content.SyncInfos[0].EntityId) == false)
        {
            return Task.CompletedTask;
        }
        
        var position = new Vector2(content.SyncInfos[^1].Position.X, content.SyncInfos[^1].Position.Y);
        var velocity = new Vector2(content.SyncInfos[^1].Velocity.X, content.SyncInfos[^1].Velocity.Y);
        
        session.Room.QueueEntityTransformUpdate(
            session.ClientId,
            content.SyncInfos[^1].EntityId,
            position,
            velocity,
            content.SyncInfos[^1].Rotation);

        return Task.CompletedTask;
    }
}