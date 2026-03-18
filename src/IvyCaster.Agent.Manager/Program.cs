using Avalonia;
using System;
using System.Threading.Tasks;
using System.Threading;
using IvyCaster.Agent.Manager.Services;

namespace IvyCaster.Agent.Manager;

class Program
{
    private static Mutex? _singleInstanceMutex;

    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    [STAThread]
    public static void Main(string[] args)
    {
        if (TryGetElevatedOperation(args, out var operation))
        {
            var exitCode = RunElevatedOperationAsync(operation).GetAwaiter().GetResult();
            Environment.Exit(exitCode);
            return;
        }

        if (!TryAcquireSingleInstance())
        {
            return;
        }

        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    private static bool TryAcquireSingleInstance()
    {
        _singleInstanceMutex = new Mutex(true, @"Global\IvyCaster.Agent.Manager.Singleton", out var createdNew);
        return createdNew;
    }

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();

    private static bool TryGetElevatedOperation(string[] args, out string operation)
    {
        operation = string.Empty;
        var index = Array.IndexOf(args, "--elevated-op");
        if (index < 0 || index + 1 >= args.Length)
        {
            return false;
        }

        operation = args[index + 1].Trim().ToLowerInvariant();
        return !string.IsNullOrWhiteSpace(operation);
    }

    private static async Task<int> RunElevatedOperationAsync(string operation)
    {
        var client = new AgentManagementClient();
        var response = operation switch
        {
            "stop" => await client.StopAsync(),
            "restart" => await client.RestartAsync(),
            _ => null
        };

        if (response is null)
        {
            return 2;
        }

        return response.Success ? 0 : 1;
    }
}
