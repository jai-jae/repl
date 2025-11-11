namespace Repl.Server.Game.ConnectionHandshake.Jobs;

internal enum HandshakeManagerJobType
{
    ConnectionEstablished,
    ConnectionClosed,
    HandshakePacket,
    CleanupExpired
}