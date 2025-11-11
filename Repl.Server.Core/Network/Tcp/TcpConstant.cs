namespace Repl.Server.Core.Network.Tcp;

public static class TcpConstant
{
    /*
     * MTU == 1500
     * IP_HEADER = 20
     * TCP_HEADER + Timestamp option = 20 + 12
     * MSS = 1500 - 20 - 32 = 1448
     */
    public const int TCP_MAX_SEGMENT_SIZE = 1448;
}