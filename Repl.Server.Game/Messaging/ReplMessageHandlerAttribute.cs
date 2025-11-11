using static ReplGameProtocol.C2GSProtocol.Types;

namespace Repl.Server.Game.MessageHandlers;

[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = true)]
public sealed class ReplMessageHandlerAttribute : Attribute
{
    public OpCode OpCode { get; init; }
    public ReplMessageHandlerAttribute(OpCode opCode)
    {
        OpCode = opCode;
    }
}