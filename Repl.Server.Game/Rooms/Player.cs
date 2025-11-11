using Repl.Server.Game.Entities;
using Repl.Server.Game.Network;

namespace Repl.Server.Game.Rooms;

public class Player
{
    public long ClientId => this.Session.ClientId;
    public long EntityId => this.Entity.ClientId;
    public PlayerEntity Entity { get; }
    public ReplGameSession Session { get; }

    public Player(ReplGameSession session, PlayerEntity entity)
    {
        Session = session;
        Entity = entity;
    }
}