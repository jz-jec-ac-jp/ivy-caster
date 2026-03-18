using System.Diagnostics;
using System.ComponentModel;
using System.Runtime.Versioning;
using System.ServiceProcess;
using IvyCaster.Agent.Runtime;
using IvyCaster.Core;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Hosting.WindowsServices;
using Microsoft.Win32;

namespace IvyCaster.Agent.Platform.Windows;

[SupportedOSPlatform("windows")]
public sealed class WindowsAgentServiceController(
    IHostApplicationLifetime lifetime,
    AgentRuntimeState runtimeState) : IAgentServiceController
{
    private const string ServiceNameEnv = "IVYCASTER_AGENT_SERVICE_NAME";
    private const string RegistryServicePath = @"Software\IvyContainer\IvyCaster";
    private const string RegistryServiceNameValue = "ServiceName";

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
                string serviceName;
                try
                {
                    serviceName = ResolveServiceName();
                }
                catch (InvalidOperationException ex)
                {
                    Trace.TraceError($"Restart aborted: {ex}");
                    return Task.FromResult(new ManagementOperationResult(
                        false,
                        ex.Message));
                }

                var helperStarted = StartServiceRestartHelper(serviceName);
                if (!helperStarted)
                {
                    return Task.FromResult(new ManagementOperationResult(
                        false,
                        $"Restart failed: could not launch restart helper for {serviceName}."));
                }

                if (!TryValidateServiceForRestart(serviceName, out var validationError))
                {
                    Trace.TraceError($"Restart aborted for service '{serviceName}': {validationError}");
                    return Task.FromResult(new ManagementOperationResult(false, $"Restart failed: {validationError}"));
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

        var registryConfigured = TryReadServiceNameFromRegistry();
        if (!string.IsNullOrWhiteSpace(registryConfigured))
        {
            return registryConfigured;
        }

        throw new InvalidOperationException(
            $"Service operation aborted: service name is not configured. Set {ServiceNameEnv} or HKLM\\{RegistryServicePath}\\{RegistryServiceNameValue}.");
    }

    private static string? TryReadServiceNameFromRegistry()
    {
        try
        {
            var from64 = TryReadServiceNameFromRegistryView(RegistryView.Registry64);
            if (!string.IsNullOrWhiteSpace(from64))
            {
                return from64;
            }

            return TryReadServiceNameFromRegistryView(RegistryView.Registry32);
        }
        catch (Exception ex)
        {
            Trace.TraceWarning($"Failed to read service name from registry: {ex}");
            return null;
        }
    }

    private static string? TryReadServiceNameFromRegistryView(RegistryView view)
    {
        using var baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, view);
        using var key = baseKey.OpenSubKey(RegistryServicePath, writable: false);
        return key?.GetValue(RegistryServiceNameValue) as string;
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

    private static bool TryValidateServiceForRestart(string serviceName, out string error)
    {
        error = string.Empty;

        try
        {
            using var service = new ServiceController(serviceName);
            var status = service.Status;

            if (status != ServiceControllerStatus.Stopped && !service.CanStop)
            {
                error = $"Service '{serviceName}' is in state '{status}' and cannot be stopped.";
                return false;
            }

            return true;
        }
        catch (Exception ex) when (ex is InvalidOperationException or Win32Exception)
        {
            error = $"Service '{serviceName}' does not exist or cannot be queried: {ex.Message}";
            return false;
        }
    }
}
