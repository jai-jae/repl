namespace Repl.Server.Core.Logging;

using Microsoft.Extensions.Logging;

public static class Log
{
    public static ILoggerFactory? Factory { get; private set; }

    public static void Initialize(ILoggerFactory factory)
    {
        if (Factory != null)
        {
            throw new InvalidOperationException("AppLogging has already been initialized.");
        }
        Factory = factory;
    }

    public static ILogger<T> CreateLogger<T>()
    {
        if (Factory == null)
        {
            throw new InvalidOperationException("AppLogging has not been initialized. Call AppLogging.Initialize() at startup.");
        }

        return Factory.CreateLogger<T>();
    }
}