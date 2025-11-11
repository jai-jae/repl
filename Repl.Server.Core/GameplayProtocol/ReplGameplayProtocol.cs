using System.Collections.Frozen;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using Google.Protobuf;
using Microsoft.Extensions.Logging;
using Repl.Server.Core.Logging;
using Repl.Server.Core.NetBuffers;
using Repl.Server.Core.Network;
using Repl.Server.Core.Network.Tcp;

namespace Repl.Server.Core.ReplProtocol;

public sealed class ReplGameplayProtocol<TClientPacket, TServerPacket> : INetProtocol
{
    private static readonly ILogger<ReplGameplayProtocol<TClientPacket, TServerPacket>> logger = Log.CreateLogger<ReplGameplayProtocol<TClientPacket, TServerPacket>>();

    private const string ProtocolNamespace = "ReplGameProtocol.";
    private const string OpcodePostfix = "+Types+OpCode";
    private const string ProtocolPostfix = "+Types+Packet+Types";
    private const ushort INVALID_MESSAGE_ID = 0;
    
    private static string ClientProtocolOpCode => ProtocolNamespace + typeof(TClientPacket).Name + OpcodePostfix;
    private static string ServerProtocolOpCode => ProtocolNamespace + typeof(TServerPacket).Name + OpcodePostfix;
    private static string ClientProtocolType => ProtocolNamespace + typeof(TClientPacket).Name + ProtocolPostfix;
    private static string ServerProtocolType => ProtocolNamespace + typeof(TServerPacket).Name + ProtocolPostfix;

    private readonly FrozenDictionary<Type, ushort> serverPacketOpCodeMap;
    private readonly FrozenDictionary<Type, ushort> clientPacketOpCodeMap;
    private readonly FrozenDictionary<ushort, MessageParser> clientPacketParserMap;
    
    public static ReplGameplayProtocol<TClientPacket, TServerPacket>? TryCreate()
    {
        Assembly? assembly = Assembly.GetExecutingAssembly();

        if (InitializeClientProtocol(assembly, out var clientMessageOpCodeMap, out var clientMessageParserMap) == false)
        {
            return null;
        }

        if (InitializeServerProtocol(assembly, out var serverMessageOpCodeMap) == false)
        {
            return null;
        }

        return new ReplGameplayProtocol<TClientPacket, TServerPacket>(serverMessageOpCodeMap, clientMessageOpCodeMap, clientMessageParserMap);
    }


    public ushort GetClientPacketOpCode(Type type)
    {
        if (clientPacketOpCodeMap.TryGetValue(type, out var opCode) == false)
        {
            logger.LogError("{type} is not registered protocol.", type);
            return INVALID_MESSAGE_ID;
        }
        return opCode;
    }

    public ushort GetServerPacketOpCode(Type type)
    {
        if (serverPacketOpCodeMap.TryGetValue(type, out var opCode) == false)
        {
            logger.LogError("{type} is not registered protocol.", type);
            return INVALID_MESSAGE_ID;
        }
        return opCode;
    }

    public ushort GetOpCode<T>() where T : IMessage<T>
    {
        if (serverPacketOpCodeMap.TryGetValue(typeof(T), out var opCode) == false)
        {
            logger.LogError("{type} is not registered protocol.", typeof(T));
            return INVALID_MESSAGE_ID;
        }
        return opCode;
    }

    public bool Validate<T>(T message, out ushort opCode, out int requiredSize) where T : IMessage<T>
    {
        opCode = this.GetOpCode<T>();
        if (opCode == INVALID_MESSAGE_ID)
        {
            requiredSize = 0;
            return false;
        }

        requiredSize = ReplPacketHeader.HEADER_SIZE + message.CalculateSize();

        if (requiredSize > TcpConstant.TCP_MAX_SEGMENT_SIZE)
        {
            requiredSize = 0;
            return false;
        }

        return true;
    }

    public bool Serialize<T>(T message, [NotNullWhen(true)] out SendBuffer? buffer) where T : IMessage<T>
    {
        if (this.Validate(message, out ushort opCode, out int requiredSize) == false)
        {
            buffer = null;
            return false;
        }

        buffer = SendBuffer.Rent(requiredSize); 

        ReplPacketHeader.WriteHeader(buffer.WriteSegment, (ushort)requiredSize, opCode);
        message.WriteTo(buffer.WriteSegment.Slice(ReplPacketHeader.HEADER_SIZE));
        return true;
    }

    public bool Serialize<T>(T message, Span<byte> buffer) where T : IMessage<T>
    {
        return false;
    }

    public bool Deserialize(ushort opCode, ReadOnlySpan<byte> byteArray, [NotNullWhen(true)] out IMessage? message)
    {
        if (clientPacketParserMap.TryGetValue(opCode, out var parser) == false)
        {
            logger.LogError("Not exists parser. opCode: {opCode}", opCode);
            message = null;
            return false;
        }

        try
        {
            message = parser.ParseFrom(byteArray);
            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex.ToString());
            message = null;
            return false;
        }
    }

    private ReplGameplayProtocol(IDictionary<Type, ushort> serverMessageOpCodeLookupTable, IDictionary<Type, ushort> clientMessageOpCodeLookupTable, IDictionary<ushort, MessageParser> clientMessageParserLookupTable)
    {
        this.serverPacketOpCodeMap = serverMessageOpCodeLookupTable.ToFrozenDictionary();
        this.clientPacketOpCodeMap = clientMessageOpCodeLookupTable.ToFrozenDictionary();
        this.clientPacketParserMap = clientMessageParserLookupTable.ToFrozenDictionary();
    }

    private static bool InitializeClientProtocol(Assembly assembly, [NotNullWhen(true)] out IDictionary<Type, ushort>? messageOpCodeMap, [NotNullWhen(true)] out IDictionary<ushort, MessageParser>? messageParserMap)
    {
        messageOpCodeMap = new Dictionary<Type, ushort>();
        messageParserMap = new Dictionary<ushort, MessageParser>();
        var typess = assembly.GetTypes();
        var clientOpCode = assembly.GetType(ClientProtocolOpCode);
        if (clientOpCode == null)
        {
            logger.LogError($"{ClientProtocolOpCode} OpCode is not found. Check Protocol Naming Convention");
            return false;
        }

        var members = assembly.GetType(ClientProtocolType)?.GetMembers();
        if (members == null)
        {
            logger.LogError($"{ClientProtocolType} messages are not found. Check Protocol Naming Convention");
            return false;
        }

        foreach (MemberInfo member in members)
        {
            if (member.MemberType != MemberTypes.NestedType)
            {
                continue;
            }

            var memberInfo = ((TypeInfo)member).DeclaredNestedTypes.FirstOrDefault();

            if (memberInfo == null)
            {
                logger.LogError($"{ClientProtocolType} message's TyepInfo is not found");
                return false;
            }

            if (memberInfo.DeclaringType == null)
            {
                logger.LogError($"{ClientProtocolType} message's DeclaringType is not found");
                return false;
            }

            if (Enum.TryParse(clientOpCode, memberInfo.DeclaringType.Name, out var opCode) == false)
            {
                logger.LogError($"{memberInfo.DeclaringType.Name} does not have corresponding OpCode");
                return false;
            }

            var parserProp = memberInfo.DeclaringType.GetProperty("Parser", BindingFlags.Static | BindingFlags.Public);
            if (parserProp == null)
            {
                logger.LogError($"{memberInfo.DeclaringType.Name} does not have Parser Property");
                return false;
            }

            if (messageOpCodeMap.TryAdd(memberInfo.DeclaringType, Convert.ToUInt16(opCode)) == false)
            {
                logger.LogError($"messageOpCodeMap has duplicate PacketType. Type: {memberInfo.DeclaringType.Name}");
                return false;
            }

            MessageParser? parser = (MessageParser?)parserProp.GetValue(null);
            if (parser == null)
            {
                logger.LogError($"{memberInfo.DeclaringType.Name} Parser instance is not found");
                return false;
            }

            if (messageParserMap.TryAdd(Convert.ToUInt16(opCode), parser) == false)
            {
                logger.LogError($"parserLookupTable has duplicate OpCode. Opcode: {opCode}, Type: {memberInfo.DeclaringType.Name}");
                return false;
            }

        }

        return true;
    }

    private static bool InitializeServerProtocol(Assembly assembly, [NotNullWhen(true)] out IDictionary<Type, ushort>? messageOpCodeMap)
    {
        messageOpCodeMap = new Dictionary<Type, ushort>();

        var serverOpCode = assembly.GetType(ServerProtocolOpCode);
        if (serverOpCode == null)
        {
            logger.LogError($"{ServerProtocolOpCode} OpCode is not found. Check .proto Naming convention");
            return false;
        }

        var messageTypes = assembly.GetType(ServerProtocolType)?.GetMembers();
        if (messageTypes == null)
        {
            logger.LogError($"{ServerProtocolOpCode} OpCode is not found. Check .proto Naming convention");
            return false;
        }

        foreach (var messageType in messageTypes)
        {
            var memberType = messageType.MemberType;
            if (memberType == MemberTypes.NestedType)
            {
                var messageTypeInfo = ((TypeInfo)messageType).DeclaredNestedTypes.FirstOrDefault();
                if (messageTypeInfo == null)
                {
                    logger.LogError($"{ServerProtocolType} message's DeclaredNestedTypes is not found");
                    return false;
                }

                if (messageTypeInfo.DeclaringType == null)
                {
                    logger.LogError($"{messageTypeInfo.Name} message's DeclaringType is not found");
                    return false;
                }

                if (Enum.TryParse(serverOpCode, messageTypeInfo.DeclaringType.Name, out var opCode) == false)
                {
                    logger.LogError($"{messageTypeInfo.DeclaringType.Name} does not have corresponding OpCode");
                    return false;
                }

                if (messageOpCodeMap.TryAdd(messageTypeInfo.DeclaringType, Convert.ToUInt16(opCode)) == false)
                {
                    logger.LogError($"messageOpCodeMap has duplicate OpCode. Type:{messageTypeInfo.DeclaringType.Name}");
                    return false;
                }
            }
        }

        return true;
    }

}