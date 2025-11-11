using Grpc.Core;
using Microsoft.Extensions.Logging;
using Repl.Server.Core.Logging;
using Repl.Server.Core.Network;
using Repl.Server.Game.Managers.Rooms;
using Repl.Server.Game.Messaging;
using Repl.Server.Game.Network;
using static ReplGameProtocol.C2GSProtocol.Types;
using static ReplGameProtocol.GS2CProtocol.Types.Packet.Types;
using static ReplInternalProtocol.Coordinator.CoordinatorService;
using static ReplInternalProtocol.DataServer.DataService;

namespace Repl.Server.Game.MessageHandlers;

[ReplMessageHandlerAttribute(OpCode.EnterGameServerRequest)]
public class EnterServerRequestHandler : GameplayMessageHandler<Packet.Types.EnterGameServerRequest>
{
    private readonly ILogger logger = Log.CreateLogger<EnterServerRequestHandler>();
    private readonly DataServiceClient dsClient;
    private readonly CoordinatorServiceClient csClient;
    private readonly RoomManager roomManager;
    private readonly INetProtocol proto;

    public EnterServerRequestHandler(INetProtocol protocol, RoomManager roomManager, DataServiceClient dsClient, CoordinatorServiceClient csClient)
    {
        this.dsClient = dsClient;
        this.csClient = csClient;
        this.roomManager = roomManager;
        this.proto = protocol;
    }

    public override async Task HandleAsync(ReplGameSession session, Packet.Types.EnterGameServerRequest content)
    {
        try
        {
            // external db query, api request, grpc request  etc...
            // Exception can happen from the library that i did not write myself.
            await Task.Delay(500);
            // result = await DB.Query(content);
        }
        catch (RpcException ex)
        {
            // handle RpcException
        }

        // TODO : external db query later.
        var accountId = Random.Shared.NextInt64();

        this.roomManager.TryGetGameRoom(1, out var room);
        var result = room.PlayerJoin(session);
        if (result)
        {
            session.CompleteEnterServer(accountId);
        }
        // Game Logic DO NOT throw Exception.
    }
}