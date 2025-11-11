using System.Data;
using Grpc.Core;
using Microsoft.Extensions.Logging;
using Repl.Server.Database.Config;
using ReplInternalProtocol.DataServer;

namespace Repl.Server.Database.Service;

public class ReplDataService : DataService.DataServiceBase
{
    private readonly MySqlConnectionFactory connectionFactory;
    private readonly ILogger<ReplDataService> logger;

    public ReplDataService(MySqlConnectionFactory connectionFactory, ILogger<ReplDataService> logger)
    {
        this.connectionFactory = connectionFactory;
        this.logger = logger;
    }
}