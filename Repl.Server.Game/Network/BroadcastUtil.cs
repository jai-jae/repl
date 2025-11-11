using Google.Protobuf;
using Repl.Server.Core.Network;
using Microsoft.Extensions.Logging;


namespace Repl.Server.Game.Network;

public static class NetworkUtility
{
    public static void Broadcast<TMessage>(
        IReadOnlyCollection<ReplGameSession> sessions,
        TMessage message,
        INetProtocol protocol,
        ILogger? logger = null) where TMessage : IMessage<TMessage>
    {
        int sessionCount = sessions.Count;
        if (sessionCount == 0)
        {
            return;
        }
        
        if (protocol.Serialize(message, out var sendBuffer) == false)
        {
            logger?.LogError("Broadcast failed: Could not serialize message of type {MessageType}.", typeof(TMessage).Name);
            return;
        }
        
        for (int i = 0; i < sessionCount - 1; i++)
        {
            sendBuffer.AddRef();
        }
        
        foreach (var session in sessions)
        {
            session.SendBuffer(sendBuffer);
            if (session.SendBuffer(sendBuffer) == false)
            {
                sendBuffer.Dispose();
            }
        }

        sendBuffer.Dispose();
    }
    
    public static void Broadcast<TMessage, TSource>(
        ICollection<TSource> sourceItems,
        Func<TSource, ReplGameSession> sessionSelector,
        TMessage message,
        INetProtocol protocol,
        ILogger? logger = null) where TMessage : IMessage<TMessage> 
    {
        int itemCount = sourceItems.Count;
        if (itemCount == 0)
        {
            return;
        }

        if (protocol.Serialize(message, out var sendBuffer) == false)
        {
            logger?.LogError("Broadcast failed: Could not serialize message of type {MessageType}.", typeof(TMessage).Name);
            return;
        }
        
        for (int i = 0; i < itemCount - 1; i++)
        {
            sendBuffer.AddRef();
        }
        
        foreach (var item in sourceItems)
        {
            var session = sessionSelector.Invoke(item);
            if (session.SendBuffer(sendBuffer) == false)
            {
                sendBuffer.Dispose();
            }
        }
        
        sendBuffer.Dispose();
    }
}
