using System.Buffers.Binary;
using System.Diagnostics.CodeAnalysis;
using System.Text;

namespace Repl.Server.Game.ConnectionHandshake.Protocol;

public class InitRequest : IHandshakeMessage
{
    public byte[] AccessToken { get; init; } = [];

    public int GetSize()
    {
        // The size is the length of the byte array.
        return this.AccessToken.Length;
    }

    public int WriteTo(Span<byte> buffer)
    {
        // Write the byte array content directly into the buffer.
        var tokenData = this.AccessToken;
        tokenData.CopyTo(buffer);
        return tokenData.Length;
    }

    /// <summary>
    /// Attempts to parse an InitRequest from the provided byte span.
    /// </summary>
    public static bool TryParse(ReadOnlySpan<byte> bytes, [NotNullWhen(true)] out InitRequest? request)
    {
        // The entire payload is the access token.
        request = new InitRequest
        {
            AccessToken = bytes.ToArray()
        };
        return true;
    }
}

public class InitResponse : IHandshakeMessage
{
    public long ChannelId { get; init; }
    public int RequiredConnections { get; init; }
    public int OptimalConnections { get; init; }
    public DateTime InitDeadline { get; init; }
    // Reordered: Variable-length field must be last for prefix-less serialization.
    public byte[] ChannelToken { get; init; } = [];

    public int GetSize()
    {
        int size = 0;
        size += sizeof(long);   // ChannelId
        size += sizeof(int);     // RequiredConnections
        size += sizeof(int);     // OptimalConnections
        size += sizeof(long);    // InitDeadline (as Ticks)
        size += this.ChannelToken.Length; // ChannelToken data
        return size;
    }

    public int WriteTo(Span<byte> buffer)
    {
        int offset = 0;
        BinaryPrimitives.WriteInt64LittleEndian(buffer.Slice(offset), this.ChannelId);
        offset += sizeof(long);

        BinaryPrimitives.WriteInt32LittleEndian(buffer.Slice(offset), this.RequiredConnections);
        offset += sizeof(int);

        BinaryPrimitives.WriteInt32LittleEndian(buffer.Slice(offset), this.OptimalConnections);
        offset += sizeof(int);

        BinaryPrimitives.WriteInt64LittleEndian(buffer.Slice(offset), this.InitDeadline.ToBinary());
        offset += sizeof(long);

        var tokenData = this.ChannelToken;
        tokenData.CopyTo(buffer.Slice(offset));
        offset += tokenData.Length;

        return offset;
    }

    public static bool TryParse(ReadOnlySpan<byte> bytes, out InitResponse? message)
    {
        message = null;
        int expectedSize = sizeof(long) + sizeof(int) +  sizeof(int) + sizeof(long) + 32;

        if (bytes.Length < expectedSize)
        {
            return false;
        }

        int offset = 0;

        long channelId = BinaryPrimitives.ReadInt64LittleEndian(bytes);
        offset += sizeof(long);

        int requiredConnection = BinaryPrimitives.ReadInt32LittleEndian(bytes.Slice(offset));
        offset += sizeof(int);
        
        int optimalConnection = BinaryPrimitives.ReadInt32LittleEndian(bytes.Slice(offset));
        offset += sizeof(int);
        
        long  initDeadline = BinaryPrimitives.ReadInt64LittleEndian(bytes.Slice(offset));
        offset += sizeof(long);
        
        byte[] channelToken = bytes.Slice(offset).ToArray();
        
        message = new InitResponse
        {
            ChannelId = channelId,
            RequiredConnections = requiredConnection,
            OptimalConnections = optimalConnection,
            InitDeadline = DateTime.FromBinary(initDeadline),
            ChannelToken = channelToken
        };
        return true;
    }
}

public class InitRejectedResponse : IHandshakeMessage
{
    public string Reason { get; init; } = string.Empty;

    public int GetSize()
    {
        return Encoding.UTF8.GetByteCount(this.Reason);
    }

    public int WriteTo(Span<byte> buffer)
    {
        return Encoding.UTF8.GetBytes(this.Reason, buffer);
    }
}

public class JoinRequest : IHandshakeMessage
{
    public long ChannelId { get; init; }
    public int ConnectionIndex { get; init; }
    // Reordered: Variable-length field must be last for prefix-less serialization.
    public byte[] ChannelToken { get; init; } = [];

    public int GetSize()
    {
        int size = 0;
        size += sizeof(long);   // ChannelId
        size += sizeof(int);     // ConnectionIndex
        size += this.ChannelToken.Length; // ChannelToken data
        return size;
    }

    public int WriteTo(Span<byte> buffer)
    {
        int offset = 0;
        BinaryPrimitives.WriteInt64LittleEndian(buffer.Slice(offset), this.ChannelId);
        offset += sizeof(long);

        BinaryPrimitives.WriteInt32LittleEndian(buffer.Slice(offset), this.ConnectionIndex);
        offset += sizeof(int);
        
        var tokenData = this.ChannelToken;
        tokenData.CopyTo(buffer.Slice(offset));
        offset += tokenData.Length;

        return offset;
    }

    /// <summary>
    /// Attempts to parse a JoinRequest from the provided byte span.
    /// </summary>
    public static bool TryParse(ReadOnlySpan<byte> bytes, [NotNullWhen(true)] out JoinRequest? request)
    {
        request = null;
        int expectedSize = sizeof(long) + sizeof(int);

        if (bytes.Length < expectedSize)
        {
            return false;
        }

        int offset = 0;

        long channelId = BinaryPrimitives.ReadInt64LittleEndian(bytes);
        offset += sizeof(long);

        int connectionIndex = BinaryPrimitives.ReadInt32LittleEndian(bytes.Slice(offset));
        offset += sizeof(int);
        
        // The rest of the payload is the token.
        byte[] channelToken = bytes.Slice(offset).ToArray();
        
        request = new JoinRequest
        {
            ChannelId = channelId,
            ConnectionIndex = connectionIndex,
            ChannelToken = channelToken
        };
        return true;
    }
}

public class JoinResponse : IHandshakeMessage
{
    public bool Success { get; set; }
    public int ConnectionIndex { get; set; }
    public int ActiveConnectionCount { get; set; }

    public int GetSize()
    {
        return sizeof(bool) + sizeof(int) + sizeof(int);
    }

    public int WriteTo(Span<byte> buffer)
    {
        int offset = 0;
        buffer[offset] = this.Success ? (byte)1 : (byte)0;
        offset += sizeof(bool);

        BinaryPrimitives.WriteInt32LittleEndian(buffer.Slice(offset), this.ConnectionIndex);
        offset += sizeof(int);

        BinaryPrimitives.WriteInt32LittleEndian(buffer.Slice(offset), this.ActiveConnectionCount);
        offset += sizeof(int);
        
        return offset;
    }

    public static bool TryParse(ReadOnlySpan<byte> bytes, [NotNullWhen(true)] out JoinResponse? response)
    {
        response = null;
        int expectedSize = sizeof(bool) + sizeof(int) + sizeof(int);
        if (bytes.Length < expectedSize)
        {
            return false;
        }
        
        int offset = 0;
        var success = bytes[0] == 1 ? true : false;
        offset += sizeof(bool);
        var connectionIndex = BinaryPrimitives.ReadInt32LittleEndian(bytes.Slice(offset));
        offset += sizeof(int);
        var activeConnectionCount = BinaryPrimitives.ReadInt32LittleEndian(bytes.Slice(offset));

        response = new JoinResponse()
        {
            Success = success,
            ConnectionIndex = connectionIndex,
            ActiveConnectionCount = activeConnectionCount
        };
        return true;
    }
}

public class ChannelReadyPacket : IHandshakeMessage
{
    public long ChannelId { get; set; }
    public int FinalConnectionCount { get; set; }
    public DateTime ServerTime { get; set; }

    public int GetSize()
    {
        return sizeof(long) + sizeof(int) + sizeof(long);
    }

    public int WriteTo(Span<byte> buffer)
    {
        int offset = 0;
        BinaryPrimitives.WriteInt64LittleEndian(buffer.Slice(offset), this.ChannelId);
        offset += sizeof(long);

        BinaryPrimitives.WriteInt32LittleEndian(buffer.Slice(offset), this.FinalConnectionCount);
        offset += sizeof(int);

        BinaryPrimitives.WriteInt64LittleEndian(buffer.Slice(offset), this.ServerTime.ToBinary());
        offset += sizeof(long);
        
        return offset;
    }
}

public class AckRequest : IHandshakeMessage
{
    public long SessionId { get; set; }
    public DateTime ClientTime { get; set; }

    public int GetSize()
    {
        return sizeof(long) + sizeof(long);
    }

    public int WriteTo(Span<byte> buffer)
    {
        int offset = 0;
        BinaryPrimitives.WriteInt64LittleEndian(buffer.Slice(offset), this.SessionId);
        offset += sizeof(long);

        BinaryPrimitives.WriteInt64LittleEndian(buffer.Slice(offset), this.ClientTime.ToBinary());
        offset += sizeof(long);

        return offset;
    }

    /// <summary>
    /// Attempts to parse an AckRequest from the provided byte span.
    /// </summary>
    public static bool TryParse(ReadOnlySpan<byte> bytes, [NotNullWhen(true)] out AckRequest? request)
    {
        request = null;
        int expectedSize = sizeof(long) + sizeof(long);

        if (bytes.Length != expectedSize)
        {
            return false;
        }

        long sessionId = BinaryPrimitives.ReadInt64LittleEndian(bytes);
        long clientTimeTicks = BinaryPrimitives.ReadInt64LittleEndian(bytes.Slice(sizeof(long)));

        request = new AckRequest
        {
            SessionId = sessionId,
            ClientTime = DateTime.FromBinary(clientTimeTicks)
        };
        return true;
    }
}

public class ReconnectRequest : IHandshakeMessage
{
    public long SessionId { get; init; }
    // Reordered: Variable-length field must be last for prefix-less serialization.
    public byte[] ReconnectToken { get; init; } = [];

    public int GetSize()
    {
        int size = 0;
        size += sizeof(long); // SessionId
        size += this.ReconnectToken.Length; // ReconnectToken data
        return size;
    }

    public int WriteTo(Span<byte> buffer)
    {
        int offset = 0;
        BinaryPrimitives.WriteInt64LittleEndian(buffer.Slice(offset), this.SessionId);
        offset += sizeof(long);

        var tokenData = this.ReconnectToken;
        tokenData.CopyTo(buffer.Slice(offset));
        offset += tokenData.Length;

        return offset;
    }

    /// <summary>
    /// Attempts to parse a ReconnectRequest from the provided byte span.
    /// </summary>
    public static bool TryParse(ReadOnlySpan<byte> bytes, [NotNullWhen(true)] out ReconnectRequest? request)
    {
        request = null;
        int expectedsize = sizeof(long);

        if (bytes.Length < expectedsize)
        {
            return false;
        }
        
        long sessionId = BinaryPrimitives.ReadInt64LittleEndian(bytes);
        
        // The rest of the payload is the token.
        byte[] reconnectToken = bytes.Slice(expectedsize).ToArray();
        
        request = new ReconnectRequest
        {
            SessionId = sessionId,
            ReconnectToken = reconnectToken
        };
        return true;
    }
}

public class ReconnectResponse : IHandshakeMessage
{
    public bool Success { get; set; }
    public int ActiveConnectionCount { get; set; }

    public int GetSize()
    {
        return sizeof(bool) + sizeof(int);
    }

    public int WriteTo(Span<byte> buffer)
    {
        int offset = 0;
        buffer[offset] = this.Success ? (byte)1 : (byte)0;
        offset += sizeof(bool);
        
        BinaryPrimitives.WriteInt32LittleEndian(buffer.Slice(offset), this.ActiveConnectionCount);
        offset += sizeof(int);

        return offset;
    }
}

public class ChannelActivated : IHandshakeMessage
{
    public long ChannelId { get; set; }

    public int GetSize()
    {
        return sizeof(long);
    }

    public int WriteTo(Span<byte> buffer)
    {
        int offset = 0;
        
        BinaryPrimitives.WriteInt64LittleEndian(buffer.Slice(offset), this.ChannelId);
        offset += sizeof(long);
        
        return offset;
    }

    public static bool TryParse(ReadOnlySpan<byte> bytes, [NotNullWhen(true)] out ChannelActivated? message)
    {
        message = null;
        var expectedSize = sizeof(long);

        if (bytes.Length != expectedSize)
        {
            return false;
        }

        message = new ChannelActivated()
        {
            ChannelId = BinaryPrimitives.ReadInt64LittleEndian(bytes),
        };
        return true;
    }
}