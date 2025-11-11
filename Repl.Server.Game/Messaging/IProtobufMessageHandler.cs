using Google.Protobuf;
using Repl.Server.Core.Network;

namespace Repl.Server.Game.MessageHandlers;

public interface IProtobufMessageHandler<in TSession> where TSession : INetworkSession
{
    Task HandleAsync(TSession session, IMessage content);
}