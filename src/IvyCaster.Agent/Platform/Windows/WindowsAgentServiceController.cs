using System.Diagnostics;
using IvyCaster.Agent.Runtime;
using IvyCaster.Core;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Hosting.WindowsServices;

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

                var helperStarted = StartServiceRestartHelper(serviceName);
                if (!helperStarted)
                {
                    return Task.FromResult(new ManagementOperationResult(
                        false,
                        $"Restart failed: could not launch restart helper for {serviceName}."));
                }

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
            var restartStartInfo = new ProcessStartInfo
            {
                FileName = path,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            foreach (var arg in currentArgs)
            {
                restartStartInfo.ArgumentList.Add(arg);
            }

            Process.Start(restartStartInfo);

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

    private static bool IsRunningAsWindowsService()
    {
        return WindowsServiceHelpers.IsWindowsService();
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

    private static bool StartServiceRestartHelper(string serviceName)
    {
        const string restartScript =
            "$serviceName = $args[0]; " +
            "sc.exe stop $serviceName | Out-Null; " +
            "$deadline = (Get-Date).AddSeconds(30); " +
            "do { " +
            "  $state = sc.exe query $serviceName | Select-String 'STATE'; " +
            "  if ($state -match 'STOPPED') { break }; " +
            "  Start-Sleep -Milliseconds 500; " +
            "} while ((Get-Date) -lt $deadline); " +
            "sc.exe start $serviceName | Out-Null;";

        var helperProcess = Process.Start(new ProcessStartInfo
        {
            FileName = GetWindowsPowerShellPath(),
            UseShellExecute = false,
            CreateNoWindow = true,
            ArgumentList =
            {
                "-NoProfile",
                "-NonInteractive",
                "-ExecutionPolicy",
                "Bypass",
                "-Command",
                restartScript,
                serviceName
            }
        });

        return helperProcess is not null;
    }

    private static string GetWindowsPowerShellPath()
    {
        var windowsDirectory = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        return Path.Combine(windowsDirectory, "System32", "WindowsPowerShell", "v1.0", "powershell.exe");
    }
}
