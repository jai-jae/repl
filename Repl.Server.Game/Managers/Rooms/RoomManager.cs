using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Repl.Server.Core.Logging;
using Repl.Server.Core.Network;
using Repl.Server.Game.Configs;
using Repl.Server.Game.Rooms;

namespace Repl.Server.Game.Managers.Rooms;


public sealed class RoomManager : IDisposable
{
    private readonly ILogger<RoomManager> logger = Log.CreateLogger<RoomManager>();
    
    private readonly ConcurrentDictionary<long, GameRoom> gameRooms = new();
    private readonly ConcurrentDictionary<long, PersonalRoom> personalRooms = new();
    private readonly RoomTickScheduler tickScheduler;
    private readonly RoomManagerOptions options;
    private readonly INetProtocol netProtocol;
    private bool disposed;
    
    public RoomManager(INetProtocol netprotocol, RoomTickScheduler tickScheduler, IOptions<RoomManagerOptions> options)
    {
        this.netProtocol = netprotocol;
        this.tickScheduler = tickScheduler;
        this.options = options.Value;
        this.CreateGameRoom(1234);
    }
    
    public PersonalRoom GetOrCreatePersonalRoom(long playerId)
    {
        return personalRooms.GetOrAdd(playerId, id =>
        {
            var room = new PersonalRoom(id);
            // Register for ticking only when active
            tickScheduler.RegisterRoom(room);
            return room;
        });
    }

    public GameRoom CreateGameRoom(int seed)
    {
        var gameRoom = new GameRoom(this.netProtocol, seed);
        this.gameRooms.TryAdd(gameRoom.Id, gameRoom);
        tickScheduler.RegisterRoom(gameRoom);
        return gameRoom;
    }

    public bool TryGetGameRoom(long roomId, out GameRoom? room)
    {
        if (this.gameRooms.TryGetValue(roomId, out room) == false)
        {
            return false;
        }
        return true;
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }
    
    private void Dispose(bool disposing)
    {
        if (this.disposed)
        {
            return;
        }

        if (disposing)
        {
            foreach (var room in this.gameRooms.Values)
            {
                room.Dispose();
            }

            foreach (var room in this.personalRooms.Values)
            {
                room.Dispose();
            }
            this.gameRooms.Clear();
            this.personalRooms.Clear();
        }

        this.disposed = true;   
    }
}
