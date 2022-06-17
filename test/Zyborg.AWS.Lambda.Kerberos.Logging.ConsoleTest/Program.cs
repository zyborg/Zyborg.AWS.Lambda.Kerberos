using Microsoft.Extensions.Logging;
using System;

namespace Zyborg.AWS.Lambda.Kerberos.Logging.ConsoleTest
{
    class Program
    {
        static ILogger Logger = new SimpleConsoleLogger(nameof(Program));

        static void Main(string[] args)
        {
            Logger.LogCritical("Hello World!");
            Logger.LogError("Hello World!");
            Logger.LogWarning("Hello World!");
            Logger.LogInformation("Hello World!");
            Logger.LogDebug("Hello World!");
            Logger.LogTrace("Hello World!");

            Logger.LogError(new NotImplementedException("Not Implemented Here!"), "Hello World!");
            Logger.LogError(new NotImplementedException("Not Implemented Here!"), null);
        }
    }
}
