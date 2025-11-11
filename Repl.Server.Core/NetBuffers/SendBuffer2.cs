namespace Repl.Server.Core.NetBuffers;

public static class SendBuffer2Helper
{
    public static ThreadLocal<SendBuffer2> CurrentBuffer = new ThreadLocal<SendBuffer2>(() => { return null; });
    // 전역이지만 나의 쓰레드에서만 고유하게 사용할 수 있는 전역

    public static int ChunkSize { get; set; } = 4096 * 100;

    public static ArraySegment<byte> Open(int reserveSize)
    {
        if (CurrentBuffer.Value == null)
            CurrentBuffer.Value = new SendBuffer2(ChunkSize);

        if (CurrentBuffer.Value.FreeSize < reserveSize)
            CurrentBuffer.Value = new SendBuffer2(ChunkSize);

        return CurrentBuffer.Value.Open(reserveSize);
    }

    public static ArraySegment<byte> Close(int usedSize)
    {
        return CurrentBuffer.Value.Close(usedSize);
    }
}

public class SendBuffer2
{
    // [u] [] [] [] [] [] [] [] [] []
    byte[] _buffer;
    int _usedSize = 0; // recvBuffer에서 wrtieSize에 해당

    public int FreeSize { get { return _buffer.Length - _usedSize; } }

    public SendBuffer2(int chunkSize)
    {
        _buffer = new byte[chunkSize];
    }

    public ArraySegment<byte> Open(int reserveSize)
    {
        if (reserveSize > FreeSize)
            return null;

        return new ArraySegment<byte>(_buffer, _usedSize, reserveSize);
    }

    public ArraySegment<byte> Close(int usedSize)
    {
        ArraySegment<byte> segment = new ArraySegment<byte>(_buffer, _usedSize, usedSize);
        _usedSize += usedSize;
        return segment;
    }
}