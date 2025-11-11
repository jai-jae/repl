namespace Repl.Server.Game.Configs;


public class GrpcClientInfo
{
    public DataServiceClientOptions dataClientOptions;
    public CoordinatorServiceClientOptions  coordinatorServiceClientOptions;
}

public class GrpcClientOptionsBase
{
    public string PrivateIp { get; set; }
    public ushort PrivateGrpcPort { get; set; }
    public int TimeoutSecond { get; set; }
}
public class DataServiceClientOptions : GrpcClientOptionsBase { }
public class CoordinatorServiceClientOptions : GrpcClientOptionsBase { }


public class RoomTickSchedulerOptions
{
    public float HighTickRate { get; set; } = 30.0f;
    public float LowTickRate { get; set; } = 10.0f;
}

public class RoomManagerOptions
{
    public int MaxRoomCount { get; set; }
    public int RoomDisposeLoopInterval { get; set; }
    public int RoomDisposeEmptyTime { get; set; }
}

public class GameServerConfig
{
    public ushort PublicPort { get; set; }
    public ushort PrivateGrpcPort { get; set; }
    public int MaxUserCount { get; set; }
    public RoomManagerOptions RoomManager { get; set; }
    public GrpcClientInfo GrpcClient { get; set; }
}
