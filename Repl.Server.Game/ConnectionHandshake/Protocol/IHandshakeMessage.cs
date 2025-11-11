namespace Repl.Server.Game.ConnectionHandshake.Protocol;

public interface IHandshakeMessage
{
    /// <summary>
    /// Calculates the exact number of bytes this message will occupy when serialized.
    /// </summary>
    int GetSize();

    /// <summary>
    /// Writes the message's fields into the provided buffer span.
    /// </summary>
    /// <returns>The number of bytes written.</returns>
    int WriteTo(Span<byte> buffer);
}