using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using Microsoft.Extensions.Logging;
using Repl.Server.Core.Logging;
using Repl.Server.Core.NetBuffers;
using Repl.Server.Core.Network.NetChannel;
using Repl.Server.Core.ReplProtocol;
using Repl.Server.Game.ConnectionHandshake.Protocol;

namespace DummyClient;

public delegate void ConnectionPacketReceivedDelegate(DummyTcpConnection connection, ushort opCode, ReadOnlySpan<byte> bytes);
public delegate void ConnectionClosedDelegate();

public static class DummyPacketBuilder
{
    public static byte[] CreatePacket<T>(ushort opCode, T message) where T : IHandshakeMessage
    {
        int messageSize = message.GetSize();
        int requiredBufferSize = ReplPacketHeader.HEADER_SIZE + messageSize;
        
        var totalPacketSize = (ushort)requiredBufferSize;
        
        var buffer = new byte[requiredBufferSize];
        ReplPacketHeader.WriteHeader(buffer, totalPacketSize, opCode);
        message.WriteTo(buffer.AsSpan(ReplPacketHeader.HEADER_SIZE));

        return buffer;
    }
    
    public static byte[] CreateGameplayAck()
    {
        int messageSize = 0;
        int requiredBufferSize = ReplPacketHeader.HEADER_SIZE;
        
        var totalPacketSize = (ushort)requiredBufferSize;
        
        var buffer = new byte[requiredBufferSize];
        ReplPacketHeader.WriteHeader(buffer, totalPacketSize, TcpMultiplexedChannel.APP_LEVEL_ACK);
        // message.WriteTo(buffer.AsSpan(ReplPacketHeader.HEADER_SIZE));

        return buffer;
    }
}


public class DummyTcpConnection : IDisposable
{
    private ILogger<DummyTcpConnection> logger = Log.CreateLogger<DummyTcpConnection>();
    
    private readonly Socket socket;
    private readonly SocketAsyncEventArgs receiveEventArgs;
    public readonly SocketAsyncEventArgs sendEventArgs;
    private readonly ReceiveBuffer receiveBuffer;

    private readonly ConcurrentQueue<SendBuffer> sendQueue = new();
    private readonly List<SendBuffer> sendPendingList = new();
    private readonly List<ArraySegment<byte>> reusableBufferList = new();
    private int isSending = 0;
    
    public int Index { get; set; }
    public EndPoint Endpoint { get; set; }
    public event ConnectionPacketReceivedDelegate? CompleteProcessPacketEvent;
    public event ConnectionClosedDelegate? ConnectionClosedEvent;

    public DummyTcpConnection(Socket socket, int index)
    {
        this.socket = socket;
        this.Index = index;
        this.receiveEventArgs = new SocketAsyncEventArgs();
        this.receiveEventArgs.Completed += this.OnReceiveCompleted;
        this.receiveBuffer = new ReceiveBuffer(4096);
        this.receiveEventArgs.SetBuffer(receiveBuffer.WriteSegment);
        
        this.sendEventArgs = new SocketAsyncEventArgs();
        this.sendEventArgs.Completed += this.OnSendCompleted;
    }

    public void Start()
    {
        this.PostReceive();
    }
    
    public void Send(ArraySegment<byte> segment)
    {
        this.sendEventArgs.SetBuffer(segment);
        var isPending = this.socket.SendAsync(this.sendEventArgs);
        if (isPending == false)
        {
            this.OnSendCompleted(this.socket, this.sendEventArgs);
        }
    }
    
    public void Send(SendBuffer sendBuffer)
    {
        sendQueue.Enqueue(sendBuffer);
        if (Interlocked.CompareExchange(ref this.isSending, 1, 0) == 0)
        {
            this.BeginSend();
        }
    }

    public void BeginSend()
    {
        if (this.socket.Connected == false)
        {
            return;
        }

        while (this.sendQueue.TryDequeue(out var buff) == true)
        {
            this.sendPendingList.Add(buff);
        }

        this.reusableBufferList.Clear();
        foreach (var sendBuffer in this.sendPendingList)
        {
            this.reusableBufferList.Add(sendBuffer.Data);
        }

        this.sendEventArgs.BufferList = this.reusableBufferList;

        try
        {
            bool pending = this.socket.SendAsync(this.sendEventArgs);
            if (pending == false)
            {
                OnSendCompleted(this.socket, this.sendEventArgs);
            }
        }
        catch (Exception ex)
        {
            this.logger.LogError(ex, "BeginSend failed. Exception");
            this.ForceClose();
        }
    }
    
    private void OnSendCompleted(object? sender, SocketAsyncEventArgs args)
    {
        try
        {
            foreach (var message in this.sendPendingList)
            {
                message.Dispose();
            }
            this.sendPendingList.Clear();
            this.sendEventArgs.BufferList = null;
            
            if (args.BytesTransferred <= 0)
            {
                this.ForceClose();
                return;
            }

            if (args.SocketError != SocketError.Success)
            {
                this.ForceClose();
                return;
            }
			
            if (this.sendQueue.IsEmpty == false)
            {
                this.BeginSend();
            }
            else
            {
                Interlocked.Exchange(ref this.isSending, 0);
            }
        }
        catch (Exception ex)
        {
            this.logger.LogError(ex, "OnSendCompletedFailed");
            this.ForceClose();
        }
        finally
        {
        }
    }


    private void PostReceive()
    {
        if (socket.Connected == false)
        {
            return;
        }

        this.receiveBuffer.Reset();

        this.receiveEventArgs.SetBuffer(this.receiveBuffer.WriteSegment);

        bool pending = this.socket.ReceiveAsync(this.receiveEventArgs);
        if (pending == false)
        {
            this.OnReceiveCompleted(this.socket, receiveEventArgs);
        }
    }

    private void OnReceiveCompleted(object? sender, SocketAsyncEventArgs args)
    {
        if (args.SocketError == SocketError.Success && args.BytesTransferred > 0)
        {
            if (this.receiveBuffer.CommitWrite(args.BytesTransferred) == false)
            {
                this.logger.LogError("OnRecv failed. Write operation overflow");
                this.ForceClose();
                return;
            }

            int processedSize = this.ProcessPacket(this.receiveBuffer.ReadSegment);

            if (processedSize < 0 || this.receiveBuffer.DataSize < processedSize)
            {
                this.logger.LogError("Process Packet failed.");
                this.ForceClose();
                return;
            }

            if (this.receiveBuffer.CommitRead(processedSize) == false)
            {
                this.logger.LogError("OnReceive failed. Read operation overflow");
                this.ForceClose();
                return;
            }
            this.PostReceive();
        }
        else
        {
            ForceClose();
        }
    }
    
    public virtual int ProcessPacket(ReadOnlySpan<byte> buffer)
    {
        int bytesProcessed = 0;
        while (true)
        {
            if (buffer.Length < ReplPacketHeader.HEADER_SIZE)
            {
                break;
            }

            ushort totalPacketSize = ReplPacketHeader.ParsePacketSize(buffer);
            var contentSize = totalPacketSize - ReplPacketHeader.HEADER_SIZE;

            if (buffer.Length < contentSize)
            {
                break;
            }

            var opCode = ReplPacketHeader.ParseOpCode(buffer);

            if (totalPacketSize > buffer.Length)
            {
                this.logger.LogError($"Content size too large. opCode:{opCode}, size:{totalPacketSize}");
                return -1;
            }

            if (totalPacketSize < ReplPacketHeader.HEADER_SIZE)
            {
                this.logger.LogError($"invalid serialized content. content is short. opCode: {opCode}, size:{totalPacketSize}");
                return -1;
            }

            CompleteProcessPacketEvent?.Invoke(this, opCode, buffer.Slice(ReplPacketHeader.HEADER_SIZE, totalPacketSize - ReplPacketHeader.HEADER_SIZE));

            bytesProcessed += totalPacketSize;
            buffer = buffer.Slice(totalPacketSize);
        }
        return bytesProcessed;
    }
    
    public void ForceClose()
    {
        if (socket.Connected)
        {
            socket.Shutdown(SocketShutdown.Both);
            socket.Close();
            ConnectionClosedEvent?.Invoke();
        }
    }

    public void Dispose()
    {
        receiveEventArgs.Dispose();
        sendEventArgs.Dispose();
        socket.Dispose();
    }
}
