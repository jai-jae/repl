using Repl.Server.Core.NetBuffers;
using Repl.Server.Core.Network.NetChannel;

namespace Repl.Server.Core.Network.Tcp;

using System;
using System.Collections.Concurrent;
using System.Net.Sockets;
using System.Threading;
using Microsoft.Extensions.Logging;
using Repl.Server.Core.Logging;

public abstract class TcpConnectionBase : IDisposable
{
	protected ILogger<TcpConnectionBase> logger = Log.CreateLogger<TcpConnectionBase>();
	
	private readonly Socket socket;
	private readonly SocketAsyncEventArgs receiveEventArgs;
	private readonly SocketAsyncEventArgs sendEventArgs;

	private readonly ReceiveBuffer receiveBuffer;

	private readonly ConcurrentQueue<SendBuffer> sendQueue = new();
	private readonly List<SendBuffer> sendPendingList = new();
	private readonly List<ArraySegment<byte>> reusableBufferList = new();
	private int isSending = 0;

	private bool isClosed = true;
	private int disposed = 0;

	public string RemoteEndpoint { get; init; }
	protected event Action? ClosedEvent;

	public abstract int ProcessPacket(ReadOnlySpan<byte> buffer);
        
	public TcpConnectionBase(Socket socket, int receiveBufferSize)
	{
		this.socket = socket;
		this.socket.NoDelay = true;
		this.socket.LingerState = new LingerOption(true, 0);
		this.socket.Blocking = false;

		this.RemoteEndpoint = socket.RemoteEndPoint?.ToString() ?? "socket_disposed";
		this.receiveBuffer = new ReceiveBuffer(receiveBufferSize);

		this.receiveEventArgs = new SocketAsyncEventArgs(); // socketAsyncEventArgsPool.Pop();
		this.receiveEventArgs.SetBuffer(this.receiveBuffer.WriteSegment);
		this.receiveEventArgs.Completed += this.OnReceiveCompleted;

		this.sendEventArgs = new SocketAsyncEventArgs(); // socketAsyncEventArgsPool.Pop();
		this.sendEventArgs.Completed += this.OnSendCompleted;
	}

	public bool Start()
	{
		this.isClosed = false;
		this.BeginReceive();

		return true;
	}

	public bool IsClosed()
	{
		if (this.disposed == 1 || this.socket is null)
		{
			this.isClosed = true;
		}

		return this.isClosed;
	}

	private void BeginReceive()
	{
		if (this.IsClosed() == true)
		{
			return;
		}

		bool pending = this.socket.ReceiveAsync(this.receiveEventArgs);
		if (pending == false)
		{
			this.OnReceiveCompleted(this.socket, receiveEventArgs);
		}
	}

	public void Send(SendBuffer sendBuffer)
	{
		if (this.IsClosed() == true)
		{
			sendBuffer.Dispose();
			return;
		}
		
		sendQueue.Enqueue(sendBuffer);
		
		if (Interlocked.CompareExchange(ref this.isSending, 1, 0) == 0)
		{
			this.BeginSend();
		}
	}

	public void Send(List<SendBuffer> sendBufferList)
	{
		if (sendBufferList.Count == 0)
		{
			this.logger.LogWarning("Connection tries to send empty bufferList.");
			return;
		}
		
		if (this.IsClosed() == true)
		{
			foreach (var sendBuffer in sendBufferList)
			{
				sendBuffer.Dispose();
			}
			return;
		}

		foreach (SendBuffer sendBuffer in sendBufferList)
		{
			this.sendQueue.Enqueue(sendBuffer);
		}

		if (Interlocked.CompareExchange(ref this.isSending, 1, 0) == 0)
		{
			this.BeginSend();
		}
	}

	private void BeginSend()
	{
		if (this.IsClosed() == true)
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

	private void OnSendCompleted(object? socket, SocketAsyncEventArgs args)
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
			
			if (this.sendQueue.IsEmpty == true)
			{
				Interlocked.Exchange(ref this.isSending, 0);
        
				if (this.sendQueue.IsEmpty == false)
				{
					if (Interlocked.CompareExchange(ref this.isSending, 1, 0) == 0)
					{
						this.BeginSend();
					}
				}
			}
			else
			{
				BeginSend();
			}
		}
		catch (Exception ex)
		{
			this.logger.LogError(ex, "OnSendCompletedFailed");
			this.ForceClose();
		}
	}
        
	private void OnClose()
	{
		this.isClosed = true;
		this.ClosedEvent?.Invoke();
		this.logger.LogDebug("Connection closed successfully.");

	}

	private void OnReceiveCompleted(object? socket, SocketAsyncEventArgs args)
	{
		if (this.IsClosed() == true)
		{
			this.logger.LogError("OnRecv failed. Session is already closed.");
			return;
		}

		if (args.SocketError != SocketError.Success)
		{
			this.ForceClose();
			return;
		}

		var transferredBytes = args.BytesTransferred;
		if (transferredBytes <= 0)
		{
			this.ForceClose();
			return;
		}

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
		
		this.receiveBuffer.Reset();
		this.receiveEventArgs.SetBuffer(this.receiveBuffer.WriteSegment);

		this.BeginReceive();
	}

	public void ForceClose()
	{
		this.OnClose();
		this.Dispose();
	}

	protected virtual void Dispose(bool disposing)
	{
		if (Interlocked.CompareExchange(ref this.disposed, 1, 0) == 0)
		{
			// MANAGED RESOURCE CLEAN UP
			// 1. Managed objects that implement IDisposable
			// 2. Managed objects that consume large amounts of memory or consume limited resources
			if (disposing)
			{
				if (this.socket.Connected == true)
				{
					this.socket.Shutdown(SocketShutdown.Both);
					this.socket.Close();
					this.socket.Dispose();
				}
				this.receiveEventArgs.Completed -= this.OnReceiveCompleted;
				this.sendEventArgs.Completed -= this.OnSendCompleted;
				this.receiveBuffer.Dispose();

				while (this.sendQueue.TryDequeue(out var sendBuffer) == true)
				{
					sendBuffer.Dispose();
				}

				foreach (var sendBuffer in this.sendPendingList)
				{
					sendBuffer.Dispose();
				}
				
				// socketAsyncEventArgsPool.Push(this.receiveEventArgs);
				// socketAsyncEventArgsPool.Push(this.receiveEventArgs);
			}
			// TODO: free unmanaged resources (unmanaged objects) and override finalizer
		}
	}

	// // TODO: override finalizer only if 'Dispose(bool disposing)' has code to free unmanaged resources
	// ~ConnectionBase()
	// {
	//     // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
	//     Dispose(disposing: false);
	// }

	public void Dispose()
	{
		// Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
		Dispose(disposing: true);
		GC.SuppressFinalize(this);
	}
}