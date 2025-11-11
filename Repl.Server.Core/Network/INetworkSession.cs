namespace Repl.Server.Core.Network;

public interface INetworkSession : IDisposable
{
	public bool Start();
	public string ToLog();
}