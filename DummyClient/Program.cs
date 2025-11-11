using Microsoft.Extensions.Logging;
using Repl.Server.Core.Logging;
using Serilog.Extensions.Logging;

namespace DummyClient
{
    class Program
    {
        static async Task Main(string[] args)
        {
            // Replace with your server's IP and port
            var serverIp = "127.0.0.1";
            var serverPort = 42422;
            using ILoggerFactory factory = new SerilogLoggerFactory(SerilogConfigurer.ConfigureAppLogger());
            Log.Initialize(factory);
            
            var client = new DummyClient(serverIp, serverPort);
            await client.Start();
            
            Console.WriteLine("Press any key to exit...");
            Console.ReadKey();
        }
    }
}