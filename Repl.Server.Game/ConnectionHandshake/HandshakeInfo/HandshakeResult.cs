using Repl.Server.Game.ConnectionHandshake.Protocol;

namespace Repl.Server.Game.ConnectionHandshake.HandshakeInfo;

public class HandshakeResult
{
    public bool IsSuccess { get; init; }
    public bool ShouldDisconnect { get; init; }
    public NetChannelOpCode ResponseOpCode { get; init; }
    public IHandshakeMessage? ResponseData { get; init; }
    public string ErrorMessage { get; init; } = string.Empty;
        
    public static HandshakeResult Success(NetChannelOpCode responseOpCode = default, IHandshakeMessage? response = null)
    {
        return new HandshakeResult
        {
            IsSuccess = true,
            ResponseOpCode = responseOpCode,
            ResponseData = response
        };
    }
        
    public static HandshakeResult Fail(string error, bool disconnect)
    {
        return new HandshakeResult
        {
            IsSuccess = false,
            ShouldDisconnect = disconnect,
            ErrorMessage = error
        };
    }
        
    public static HandshakeResult Reject(NetChannelOpCode responseOpCode, IHandshakeMessage? response)
    {
        return new HandshakeResult
        {
            IsSuccess = false,
            ShouldDisconnect = false,
            ResponseOpCode = responseOpCode,
            ResponseData = response
        };
    }
}
