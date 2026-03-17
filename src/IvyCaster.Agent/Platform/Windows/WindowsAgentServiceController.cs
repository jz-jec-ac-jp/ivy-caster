using System.Diagnostics;
using IvyCaster.Agent.Runtime;
using IvyCaster.Core;
using Microsoft.Extensions.Hosting;

namespace IvyCaster.Agent.Platform.Windows;

public sealed class WindowsAgentServiceController(
    IHostApplicationLifetime lifetime,
    AgentRuntimeState runtimeState) : IAgentServiceController
{
    private const string ServiceNameEnv = "IVYCASTER_AGENT_SERVICE_NAME";

    public Task<AgentServiceState> GetStateAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(AgentServiceState.Running);
    }

    public Task<ManagementOperationResult> StopAsync(CancellationToken cancellationToken = default)
    {
        var mode = IsRunningAsWindowsService() ? "service" : "interactive";
        _ = Task.Run(async () =>
        {
            await Task.Delay(350);
            lifetime.StopApplication();
        }, CancellationToken.None);

        return Task.FromResult(new ManagementOperationResult(true, $"Stop signal sent. mode={mode}."));
    }

    public Task<ManagementOperationResult> RestartAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            if (IsRunningAsWindowsService())
            {
                var serviceName = ResolveServiceName();
                if (string.IsNullOrWhiteSpace(serviceName))
                {
                    return Task.FromResult(new ManagementOperationResult(
                        false,
                        $"Restart failed: service name is unavailable. Set {ServiceNameEnv} or run in interactive mode."));
                }

                // Service mode: request start directly so it is not gated by stop result.
                var serviceRestartCommand = $"/c sc start {QuoteArgument(serviceName)}";
                Process.Start(new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = serviceRestartCommand,
                    UseShellExecute = false,
                    CreateNoWindow = true
                });

                lifetime.StopApplication();

                return Task.FromResult(new ManagementOperationResult(
                    true,
                    $"Service restart scheduled for {serviceName}. mode=service."));
            }

            var path = Environment.ProcessPath;
            if (string.IsNullOrWhiteSpace(path))
            {
                return Task.FromResult(new ManagementOperationResult(false, "Process path is unavailable."));
            }

            var currentArgs = Environment.GetCommandLineArgs().Skip(1).ToArray();
            var joinedArgs = string.Join(" ", currentArgs.Select(QuoteArgument));
            var restartCommand = string.IsNullOrWhiteSpace(joinedArgs)
                ? $"/c timeout /t 1 /nobreak >nul && start \"\" \"{path}\""
                : $"/c timeout /t 1 /nobreak >nul && start \"\" \"{path}\" {joinedArgs}";

            Process.Start(new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = restartCommand,
                UseShellExecute = false,
                CreateNoWindow = true
            });

            _ = Task.Run(async () =>
            {
                await Task.Delay(150);
                lifetime.StopApplication();
            }, CancellationToken.None);

            return Task.FromResult(new ManagementOperationResult(true, $"Restart scheduled for {runtimeState.AgentId}. mode=interactive."));
        }
        catch (Exception ex)
        {
            return Task.FromResult(new ManagementOperationResult(false, $"Restart failed: {ex.Message}"));
        }
    }

    private static string QuoteArgument(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return "\"\"";
        }

        if (!value.Contains(' ') && !value.Contains('"'))
        {
            return value;
        }

        var escaped = value.Replace("\"", "\\\"");
        return $"\"{escaped}\"";
    }

    private static bool IsRunningAsWindowsService()
    {
        // In dev/admin manual run, UserInteractive is usually true.
        // In service host context, it is false.
        return !Environment.UserInteractive;
    }

    private static string ResolveServiceName()
    {
        var configured = Environment.GetEnvironmentVariable(ServiceNameEnv);
        if (!string.IsNullOrWhiteSpace(configured))
        {
            return configured;
        }

        var processName = Process.GetCurrentProcess().ProcessName;
        return processName;
    }
}
