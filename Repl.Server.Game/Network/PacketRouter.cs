using System.Collections.Frozen;
using Google.Protobuf;
using Microsoft.Extensions.Logging;
using Repl.Server.Core.Logging;
using Repl.Server.Core.Network;
using Repl.Server.Game.MessageHandlers;

namespace Repl.Server.Game.Network;

public class PacketRouter<TSession> where TSession : INetworkSession
{
    private readonly ILogger logger = Log.CreateLogger<PacketRouter<TSession>>();

    private readonly FrozenDictionary<ushort, IProtobufMessageHandler<TSession>> handlers;
        
    public PacketRouter(Dictionary<ushort, IProtobufMessageHandler<TSession>> handlerMap)
    {
        this.handlers = handlerMap.ToFrozenDictionary();
    }

    public void OnReceivedGameplayPacket(TSession sender, ushort opCode, IMessage content)
    {
        if (this.handlers.TryGetValue(opCode, out var handler) == false)
        {
            logger.LogError($"Invalid message handler. Session:{sender.ToLog()}, OpCode:{opCode}");
            sender.Dispose();
            return;
        }

        var task = handler.HandleAsync(sender, content);
        if (task.IsCompleted == false)
        {
            task.ContinueWith(
                static (t, context) =>
                {
                    var (opCode, sender, instance) = ((ushort, TSession, PacketRouter<TSession>))context!;
                    instance.HandleException(t, sender, opCode);
                },
                (opCode, sender, this),
                TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously
            );
        }
        else if (task.IsFaulted)
        {
            HandleException(task, sender, opCode);
        }
    }
        
    private void HandleException(Task task, TSession sender, ushort opCode)
    {
        var exception = task.Exception?.GetBaseException();
    
        logger.LogError(exception, 
            "Message handler failed. Session:{Session}, Exception:{ExceptionType}", 
            sender.ToLog(), exception?.GetType().Name);
            
        // Environment.FailFast(exception?.StackTrace);
    }
}