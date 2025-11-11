using System.Net;
using System.Reflection;
using Grpc.Core;
using Grpc.Net.Client.Configuration;
using Grpc.Net.ClientFactory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Repl.Server.Core.GrpcInterceptors;
using Repl.Server.Core.Network;
using Repl.Server.Core.ReplProtocol;
using Repl.Server.Game.Configs;
using Repl.Server.Game.Managers.Rooms;
using Repl.Server.Game.MessageHandlers;
using Repl.Server.Game.Network;
using ReplGameProtocol;
using ReplInternalProtocol.Coordinator;
using ReplInternalProtocol.DataServer;

namespace Repl.Server.Game.StartupExtension;

public static class GameServerServiceExtension
{
    public static IServiceCollection AddGameServerServices(this IServiceCollection services)
    {
        AddGameplayProtocol(services);
        AddGameplayMessageHandlers(services);

        var config = services.BuildServiceProvider().GetRequiredService<IConfiguration>();
        services.Configure<GameServerConfig>(config.GetSection("GameServerContext"));
        services.Configure<RoomManagerOptions>(config.GetSection("GameServerContext:RoomManager"));
        services.Configure<RoomTickSchedulerOptions>(config.GetSection("GameServerContext:RoomManager:RoomTickScheduler"));
        services.AddSingleton<RoomManager>();
        services.AddSingleton<RoomTickScheduler>();
        services.AddSingleton<GameServer>();

        services.AddHostedService<GameServer>(provider => provider.GetRequiredService<GameServer>());
        services.AddHostedService<GameServerRegistrationService>();

        return services;
    }

    public static void AddInternalGrpcClients(this IServiceCollection services)
    { 
        var config = services.BuildServiceProvider().GetRequiredService<IConfiguration>();

        var coordinatorServiceClientOptions = new CoordinatorServiceClientOptions();
        var dataServiceClientOptions = new DataServiceClientOptions();
        config.GetSection("GameServerContext:GrpcClient:DataService").Bind(dataServiceClientOptions);
        config.GetSection("GameServerContext:GrpcClient:CoordinatorService").Bind(coordinatorServiceClientOptions);

        services.AddGrpcClient<DataService.DataServiceClient>(options =>
        {
            options.Address = config.GetGrpcUri("DataServiceContext");
        })
        .ConfigureChannel(options =>
        {
            var methodConfig = new MethodConfig()
            {
                Names = {
                    MethodName.Default
                },
                RetryPolicy = new RetryPolicy
                {
                    MaxAttempts = 5,
                    InitialBackoff = TimeSpan.FromSeconds(0.1),
                    BackoffMultiplier = 2.0,
                    MaxBackoff = TimeSpan.FromSeconds(5),
                    RetryableStatusCodes = { StatusCode.Unavailable, StatusCode.Unknown }
                }
            };

            options.ServiceConfig = new ServiceConfig
            {
                MethodConfigs = { methodConfig }
            };
        })
        .AddInterceptor(
            InterceptorScope.Client,
            services =>
            {
                return new TimeoutInterceptor(TimeSpan.FromSeconds(dataServiceClientOptions.TimeoutSecond));
            }
        );

        services.AddGrpcClient<CoordinatorService.CoordinatorServiceClient>(options =>
        {
            options.Address = config.GetGrpcUri("CoordinatorContext");
        })
        .ConfigureChannel(options =>
        {
            var methodConfig = new MethodConfig
            {
                Names = { MethodName.Default },
                RetryPolicy = new RetryPolicy
                {
                    MaxAttempts = 10,
                    InitialBackoff = TimeSpan.FromSeconds(0.1),
                    BackoffMultiplier = 2.0,
                    MaxBackoff = TimeSpan.FromSeconds(5),
                    RetryableStatusCodes = { StatusCode.Unavailable, StatusCode.Unknown }
                }
            };

            options.ServiceConfig = new ServiceConfig
            {
                MethodConfigs = { methodConfig }
            };
        })
        .AddInterceptor(
            InterceptorScope.Client,
            services =>
            {
                return new TimeoutInterceptor(TimeSpan.FromSeconds(coordinatorServiceClientOptions.TimeoutSecond));
            }
        );
    }

    private static void AddGameplayProtocol(IServiceCollection services)
    {
        services.AddSingleton<INetProtocol>(provider =>
        {
            var protocol = ReplGameplayProtocol<C2GSProtocol, GS2CProtocol>.TryCreate();
            if (protocol is null)
            {
                throw new ApplicationException("Could not create REPL protocol");
            }
            return protocol;
        });
        
    }

    private static void AddGameplayMessageHandlers(IServiceCollection services)
    {
        services.AddSingleton<PacketRouter<ReplGameSession>>(provider =>
        {
            var handlerTypes = Assembly.GetExecutingAssembly().GetTypes().Where(
                type => type.IsClass &&
                        !type.IsAbstract &&
                        typeof(IProtobufMessageHandler<ReplGameSession>).IsAssignableFrom(type)
            );

            var handlerMap = new Dictionary<ushort, IProtobufMessageHandler<ReplGameSession>>();
            foreach (var handlerType in handlerTypes)
            {
                var attribute = handlerType.GetCustomAttribute<ReplMessageHandlerAttribute>();
                if (attribute == null)
                {
                    throw new ApplicationException($"Handler:{handlerType.Name} is not annotated.");
                }
                var handlerInstance = (IProtobufMessageHandler<ReplGameSession>)ActivatorUtilities.CreateInstance(provider, handlerType);

                if (!handlerMap.TryAdd((ushort)attribute.OpCode, handlerInstance))
                {
                    throw new ApplicationException($"Duplicate handler for opCode {attribute.OpCode}");
                }
            }
            return new PacketRouter<ReplGameSession>(handlerMap);
        });
    }
    
    private static Uri GetGrpcUri(this IConfiguration configuration, string sectionName)
    {
        var section = configuration.GetSection(sectionName);
        var ip = section["PrivateIp"] ?? IPAddress.Loopback.ToString();
        var port = section["PrivateGrpcPort"];
        if (string.IsNullOrEmpty(port))
        {
            throw new InvalidOperationException($"PrivateGrpcPort not found in {sectionName}");
        }
        return new Uri($"http://{ip}:{port}");
    }
}
