using System.Net;
using System.Net.Sockets;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Repl.Server.Core.Logging;
using Repl.Server.Core.Network.Tcp;

namespace Repl.Server.Core.Network;

public abstract class TcpServer<TSession> : BackgroundService where TSession : INetworkSession
{
    protected readonly ILogger<TcpServer<TSession>> logger = Log.CreateLogger<TcpServer<TSession>>();

    private enum ServerState
    {
        NotStarted = 0,
        Running,
        Stopping,
        Stopped
    }

    private readonly ushort listenPort;
    private ServerState state = ServerState.NotStarted;

    protected abstract TcpConnectionBase CreateConnection(Socket socket);
    protected abstract void AddConnection(TcpConnectionBase connection);
        
    protected TcpServer(ushort listenPort)
    {
        this.listenPort = listenPort;
    }

    protected override async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        var listener = new TcpListener(IPAddress.Any, this.listenPort);

        listener.Start();
        this.logger.LogInformation("GameServer listening for TCP Connection at port {listenPort}", listenPort);
        state = ServerState.Running;

        await using CancellationTokenRegistration registry = cancellationToken.Register(() => listener.Stop());

        while (cancellationToken.IsCancellationRequested == false)
        {
            Socket socket = await listener.AcceptSocketAsync(cancellationToken);

            socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.NoDelay, true);

            var connection = this.CreateConnection(socket);
            this.AddConnection(connection);
            connection.Start();
        }

        this.logger.LogInformation($"TCP listening stopped.");
    }

    public override Task StopAsync(CancellationToken cancellationToken)
    {
        switch (state)
        {
            case ServerState.NotStarted:
                this.logger.LogInformation("Server has not been started");
                break;
            case ServerState.Running:
                state = ServerState.Stopped;
                this.logger.LogInformation("Server was stopped");
                break;
            case ServerState.Stopped:
                this.logger.LogInformation("Server has already been stopped");
                break;
        }

        return Task.CompletedTask;
    }
}