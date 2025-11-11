using Repl.Server.Core.ReplProtocol;

using System.Net;
using System.Net.Sockets;
using Microsoft.Extensions.Logging;
using Repl.Server.Core.Logging;
using Repl.Server.Game.ConnectionHandshake.Protocol;
using ReplGameProtocol;
using static  ReplGameProtocol.C2GSProtocol.Types.Packet.Types;
namespace DummyClient
{
    public class DummyClient
    {
        private readonly string serverIp;
        private readonly int serverPort;
        private readonly ILogger<DummyClient> logger = Log.CreateLogger<DummyClient>();
        private readonly List<DummyTcpConnection> connections = new();
        private long channelId;
        private byte[] channelToken;
        private int requiredConnections;
        private ReplGameplayProtocol<GS2CProtocol, C2GSProtocol> gameplayProtocol;
        
        public DummyClient(string serverIp, int serverPort)
        {
            this.serverIp = serverIp;
            this.serverPort = serverPort;
            gameplayProtocol = ReplGameplayProtocol<GS2CProtocol, C2GSProtocol>.TryCreate();
        }

        public async Task Start()
        {
            try
            {
                // 1. Initial Connection and INIT request
                var initialConnection = await CreateAndConnectAsync(0);
                if (initialConnection == null) return;

                connections.Add(initialConnection);
                initialConnection.CompleteProcessPacketEvent += OnPacketReceived;
                initialConnection.Start();

                await SendInitRequest(initialConnection);

                // The rest of the process will be driven by server responses
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error during client startup");
            }
        }

        private async Task<DummyTcpConnection?> CreateAndConnectAsync(int index)
        {
            try
            {
                var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                await socket.ConnectAsync(new IPEndPoint(IPAddress.Parse(serverIp), serverPort));
                logger.LogInformation("Connected to server at {serverIp}:{serverPort}", serverIp, serverPort);
                return new DummyTcpConnection(socket, index);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to connect to server");
                return null;
            }
        }

        private async Task SendInitRequest(DummyTcpConnection connection)
        {
            var initRequest = new InitRequest
            {
                AccessToken = new byte[32] // A dummy access token
            };
            var sendBuffer = DummyPacketBuilder.CreatePacket((ushort)NetChannelOpCode.INIT_TCP_MULTIPLEXED_REQUEST, initRequest);
            connection.Send(sendBuffer);
            logger.LogInformation("Sent INIT request");
        }

        private void OnPacketReceived(DummyTcpConnection connection, ushort opCode, ReadOnlySpan<byte> content)
        {
            var netOpCode = (NetChannelOpCode)opCode;
            logger.LogInformation("Connection[{i}], Received packet with opcode: {netOpCode}", connection.Index, netOpCode);

            switch (netOpCode)
            {
                case NetChannelOpCode.INIT_TCP_MULTIPLEXED_RESPONSE:
                    HandleInitResponse(content);
                    break;
                case NetChannelOpCode.TCP_MULTIPLEXED_CHANNEL_READY:
                    HandleChannelReady(connection, content);
                    break;
                case NetChannelOpCode.JOIN_TCP_MULTIPLEXED_RESPONSE:
                    HandleJoinResponse(content);
                    break;
                case NetChannelOpCode.TCP_MULTIPLEXED_CHANNEL_ACTIVATED: 
                    InitiateSession();
                    break;
                // Add cases for other gameplay packets here
                default:
                    logger.LogWarning("Unhandled opcode: {netOpCode}", netOpCode);
                    break;
            }
        }

        private void HandleInitResponse(ReadOnlySpan<byte> content)
        {
            if (InitResponse.TryParse(content, out var initResponse))
            {
                this.channelId = initResponse.ChannelId;
                this.channelToken = initResponse.ChannelToken;
                this.requiredConnections = initResponse.RequiredConnections;

                logger.LogInformation("Received INIT response. ChannelId: {channelId}, RequiredConnections: {requiredConnections}", channelId, requiredConnections);

                // 1. Create and connect all remaining connections first.
                var newConnections = new List<DummyTcpConnection>();
                for (int i = 1; i < requiredConnections; i++)
                {
                    var newConnection =  CreateAndConnectAsync(i).Result;
                    if (newConnection != null)
                    {
                        newConnection.Start();
                        newConnections.Add(newConnection);
                    }
                }

                // Ensure all connections are added to the main list and have handlers attached.
                foreach (var conn in newConnections)
                {
                    connections.Add(conn);
                    conn.CompleteProcessPacketEvent += OnPacketReceived;
                    // The Start() method is already called inside CreateAndConnectAsync, so the receive loop is running.
                }

                // 2. Now that all connections are listening, send the JOIN requests.
                for (int i = 0; i < newConnections.Count; i++)
                {
                    // Connection index starts from 1 for JOIN requests.
                    SendJoinRequest(newConnections[i], i + 1);
                }
            }
            else
            {
                logger.LogError("Failed to parse INIT response");
            }
        }

        private void SendJoinRequest(DummyTcpConnection connection, int index)
        {
            var joinRequest = new JoinRequest
            {
                ChannelId = this.channelId,
                ChannelToken = this.channelToken
            };
            var sendBuffer = DummyPacketBuilder.CreatePacket((ushort)NetChannelOpCode.JOIN_TCP_MULTIPLEXED_REQUEST, joinRequest);
            connection.Send(sendBuffer);
            logger.LogInformation("Sent JOIN request for connection index {index}", index);
        }
        
        private void HandleJoinResponse(ReadOnlySpan<byte> content)
        {
            if(JoinResponse.TryParse(content, out var joinResponse))
            {
                if (joinResponse.Success)
                {
                    logger.LogInformation("Successfully joined channel with connection index {ConnectionIndex}. Active connections: {ActiveConnectionCount}", joinResponse.ConnectionIndex, joinResponse.ActiveConnectionCount);
                }
                else
                {
                    logger.LogError("Failed to join channel.");
                }
            }
        }


        private void HandleChannelReady(DummyTcpConnection connection, ReadOnlySpan<byte> content)
        {
            logger.LogInformation("Channel is ready! Sending ACK.");
            SendAck(connection);
        }

        private void SendAck(DummyTcpConnection connection)
        {
            var ackRequest = new AckRequest
            {
                SessionId = 0, // This would be the actual session ID if known
                ClientTime = DateTime.UtcNow
            };
            var sendBuffer = DummyPacketBuilder.CreatePacket((ushort)NetChannelOpCode.TCP_MULTIPLEXED_CHANNEL_READY_ACKNOWLEDGED, ackRequest);
            connection.Send(sendBuffer);
            logger.LogInformation("Sent ACK");
        }

        private void OnGameplayPacketReceived(DummyTcpConnection connection, ushort opCode, ReadOnlySpan<byte> content)
        {
            var gameplayOpCode = (ReplGameProtocol.GS2CProtocol.Types.OpCode)opCode;
            var ack = DummyPacketBuilder.CreateGameplayAck();
            connection.Send(ack);
            
            switch (gameplayOpCode)
            {
                case GS2CProtocol.Types.OpCode.EntitySnapshotUpdate:
                    HandleEntitySnapshotUpdate(content);
                    break;
                case GS2CProtocol.Types.OpCode.EnterServerRes:
                    HandleEnterGameServerResponse(content);
                    break;
            }
        }

        private void InitiateSession()
        {
            logger.LogInformation("Channel is active! Sending gameplay packets.");
            
            foreach (var connection in connections)
            {
                connection.sendEventArgs.SetBuffer(null);
                connection.CompleteProcessPacketEvent -= OnPacketReceived;
                connection.CompleteProcessPacketEvent += OnGameplayPacketReceived;
            }

            this.gameplayProtocol.Serialize(new EnterGameServerRequest(), out var buffer);
            connections[0].Send(buffer);
        }

        private void HandleEntitySnapshotUpdate(ReadOnlySpan<byte> content)
        {
            Console.WriteLine(content.ToString());
        }
        
        private void HandleEnterGameServerResponse(ReadOnlySpan<byte> content)
        {
            long tick = 1;
            while (true)
            {
                var moves = new TransformSyncInfo()
                {
                    EntityId = 1,
                    Position = new Vector2F
                    {
                        X = 1.0f,
                        Y = 1.0f
                    }
                };

                var movement = new SyncPlayerTransformUpdate()
                {
                    ClientTick = tick++,
                    SyncInfos = { moves }
                };
                this.gameplayProtocol.Serialize(movement, out var buffer);
                this.connections[0].Send(buffer);
                Thread.Sleep(3000);
            }
        }
    }
}