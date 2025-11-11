using Google.Protobuf;
using Repl.Server.Game.MessageHandlers;
using Repl.Server.Game.Network;

namespace Repl.Server.Game.Messaging;

public abstract class GameplayMessageHandler<TMessage> : IProtobufMessageHandler<ReplGameSession>
    where TMessage : IMessage
{
    public abstract Task HandleAsync(ReplGameSession session, TMessage content);

    public Task HandleAsync(ReplGameSession session, IMessage content)
    {
        return HandleAsync(session, (TMessage)content);
    }
}