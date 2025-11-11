using System.Numerics;
using Microsoft.Extensions.Logging;
using Repl.Server.Core.Network;
using Repl.Server.Core.ReplProtocol;
using Repl.Server.Game.Entities.Components;
using Repl.Server.Game.Managers.Rooms;
using Repl.Server.Game.Messaging;
using Repl.Server.Game.Network;
using ReplGameProtocol;
using static ReplGameProtocol.C2GSProtocol.Types;
using static ReplGameProtocol.GS2CProtocol.Types;
using EntitySnapshot = Repl.Server.Game.Entities.Components.EntitySnapshot;
using Vector2 = Repl.Server.Core.MathUtils.Vector2;

namespace Repl.Server.Game.MessageHandlers;

[ReplMessageHandler(C2GSProtocol.Types.OpCode.SyncPlayerTransformUpdate)]
public class SyncPlayerTransformUpdateHandler : GameplayMessageHandler<C2GSProtocol.Types.Packet.Types.SyncPlayerTransformUpdate>
{
    private readonly GameServer gs;
    private readonly RoomManager roomMgr;
    private readonly INetProtocol proto;

    public SyncPlayerTransformUpdateHandler(RoomManager roomMgr, INetProtocol protocol)
    {
        this.roomMgr = roomMgr;
        this.proto = protocol;
    }

    public override Task HandleAsync(ReplGameSession session, C2GSProtocol.Types.Packet.Types.SyncPlayerTransformUpdate content)
    {
        /*
        if (session.Room is null)
        {
            session.Close();
            return Task.CompletedTask;
        }

        if (session.PlayerEntityId.HasValue == false)
        {
            return Task.CompletedTask;
        }
        */
        float result = 0;
        foreach (var info in content.SyncInfos)
        {
            // Use the null-coalescing operator '??' to provide a default value.
            // If the property on the left is null, the value on the right is used.
            // This guarantees your local variable is never null.
            
            Vector2 currentPosition = info.Position is not null ? new Vector2(info.Position.X, info.Position.Y) : Vector2.Zero;
            Vector2 currentVelocity = info.Velocity is not null ? new Vector2(info.Velocity.X, info.Velocity.Y) : Vector2.Zero;
            Vector2 appliedForce = info.Force is not null ? new Vector2(info.Force.X, info.Force.Y) : Vector2.Zero;

            result += SimulateCpuIntensiveWork(content.ClientTick, currentPosition);
            // Console.WriteLine($"  - Position: {currentPosition}");

            // Now you can use currentPosition, currentVelocity, and appliedForce
            // in your game logic without any fear of a NullReferenceException.
            // For example:
            // ApplyPhysics(info.EntityId, currentPosition, currentVelocity, appliedForce);
        }
        
        var message = new GS2CProtocol.Types.Packet.Types.EntitySnapshotUpdate
        {
            Entities =
            {
                new ReplGameProtocol.EntitySnapshot
                {
                    EntityId = Random.Shared.NextInt64(),
                    Position = new Vector2F { X = 123, Y = 456 }
                },
                new ReplGameProtocol.EntitySnapshot
                {
                    EntityId = Random.Shared.NextInt64(),
                    Position = new Vector2F { X = 123, Y = 456 }
                },
                new ReplGameProtocol.EntitySnapshot
                {
                    EntityId = Random.Shared.NextInt64(),
                    Position = new Vector2F { X = 123, Y = 456 }
                },                new ReplGameProtocol.EntitySnapshot
                {
                    EntityId = Random.Shared.NextInt64(),
                    Position = new Vector2F { X = 123, Y = 456 }
                }
            }
        };
        /*
        NetworkUtility.Broadcast(
            this.gameServer.connectingSessions.ToList().AsReadOnly(), message, this.netProtocol);
        // Console.WriteLine($"Position: {position}, Velocity: {velocity}");
        /*
        session.Room.QueueEntityTransformUpdate(
            session.ClientId,
            session.PlayerEntityId.Value,
            position,
            velocity,
            content.SyncInfos[^1].Rotation);
*/
        return Task.CompletedTask;
    }
    
    public static float SimulateCpuIntensiveWork(long clientTick, Vector2 position)
    {
        // This is a dummy method to simulate a heavy CPU load.
        // In a real game server, this could be:
        // - A complex physics calculation.
        // - An AI pathfinding or behavior tree update.
        // - A collision check against hundreds of other objects.

        float magnitude = MathF.Sqrt(position.X * position.X + position.Y * position.Y);
        float result = 0;

        // Run a loop with some math functions to burn CPU cycles.
        // The number of iterations can be increased for a heavier load.
        for (int i = 0; i < 5000; i++)
        {
            result += MathF.Sin(magnitude + i) * MathF.Cos(magnitude - i);
        }

        return result;
        // We print the result so the compiler doesn't optimize away the loop.
        // In a real scenario, you would use the result of the calculation.
    }
}