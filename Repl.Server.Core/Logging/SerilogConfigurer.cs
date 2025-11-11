using Serilog;
using Serilog.Core;
using Serilog.Events;

namespace Repl.Server.Core.Logging;

public static class SerilogConfigurer
{
    public static Logger ConfigureAppLogger()
    {
        var logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .Enrich.WithThreadId()
            .WriteTo.Console(
                restrictedToMinimumLevel: LogEventLevel.Debug,
                outputTemplate: "{Timestamp:u}{Timestamp:.fff} {SourceContext} [{Level:u3}] <{ThreadId}> {Message}{NewLine}{Exception}}")
            .CreateLogger();

        return logger;
    }

    public static Logger ConfigureLoggerForDI()
    {
        Logger logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .MinimumLevel.Override("Microsoft", LogEventLevel.Information)
            .MinimumLevel.Override("Microsoft.Hosting", LogEventLevel.Warning)
            .MinimumLevel.Override("Microsoft.AspNetCore.Routing", LogEventLevel.Warning)
            .MinimumLevel.Override("Microsoft.AspNetCore.Hosting.Diagnostics", LogEventLevel.Warning)
            .MinimumLevel.Override("Grpc", LogEventLevel.Information)
            .Enrich.WithThreadId()
            .WriteTo.Console(
                restrictedToMinimumLevel: LogEventLevel.Debug,
                outputTemplate: "{Timestamp:u}{Timestamp:.fff} {SourceContext} [{Level:u3}] <{ThreadId}> {Message}{NewLine}{Exception}}")
            .CreateLogger();

        return logger;
    }
}