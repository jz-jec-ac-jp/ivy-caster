using System.Diagnostics;
using System.ComponentModel;
using System.IO;
using System.Reflection;
using System.Security.Principal;
using IvyCaster.Core;

namespace IvyCaster.Agent.Manager.Services;

public sealed class OperationElevationService : IPrivilegeElevationService
{
    public async Task<ElevationResult> ElevateAndExecuteAsync(string operationName, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(operationName))
        {
            return new ElevationResult(false, false, "Operation name is required.");
        }

        try
        {
            if (IsElevated())
            {
                var operationResponse = await ExecuteOperationAsync(operationName, cancellationToken);
                return new ElevationResult(operationResponse.Success, false, operationResponse.Message);
            }

            var processPath = Environment.ProcessPath;
            if (string.IsNullOrWhiteSpace(processPath))
            {
                return new ElevationResult(false, false, "Current executable path is unavailable.");
            }

            var startInfo = BuildElevationStartInfo(processPath, operationName);
            using var process = Process.Start(startInfo);
            if (process is null)
            {
                return new ElevationResult(false, false, "Failed to start elevated process.");
            }

            await process.WaitForExitAsync(cancellationToken);
            if (process.ExitCode == 0)
            {
                return new ElevationResult(true, false, "Elevated operation completed.");
            }

            return new ElevationResult(false, false, $"Elevated process exited with code {process.ExitCode}.");
        }
        catch (Exception ex) when (ex is Win32Exception { NativeErrorCode: 1223 })
        {
            return new ElevationResult(false, true, "User cancelled elevation.");
        }
        catch (Exception ex)
        {
            return new ElevationResult(false, false, ex.Message);
        }
    }

    private static async Task<AgentManagementResponse> ExecuteOperationAsync(string operationName, CancellationToken cancellationToken)
    {
        var normalizedOperation = operationName.Trim().ToLowerInvariant();
        var client = new AgentManagementClient();

        return normalizedOperation switch
        {
            "stop" => await client.StopAsync(cancellationToken),
            "restart" => await client.RestartAsync(cancellationToken),
            _ => new AgentManagementResponse(false, $"Unsupported operation: {operationName}")
        };
    }

    private static ProcessStartInfo BuildElevationStartInfo(string processPath, string operationName)
    {
        var entryAssemblyPath = Assembly.GetEntryAssembly()?.Location;
        var isDotnetHost = string.Equals(
            Path.GetFileNameWithoutExtension(processPath),
            "dotnet",
            StringComparison.OrdinalIgnoreCase);

        var invocationArgs = $"--elevated-op {operationName}";
        if (isDotnetHost && !string.IsNullOrWhiteSpace(entryAssemblyPath) && File.Exists(entryAssemblyPath))
        {
            invocationArgs = $"\"{entryAssemblyPath}\" --elevated-op {operationName}";
        }

        if (OperatingSystem.IsWindows())
        {
            return new ProcessStartInfo
            {
                FileName = processPath,
                Arguments = invocationArgs,
                UseShellExecute = true,
                Verb = "runas"
            };
        }

        if (OperatingSystem.IsLinux())
        {
            return new ProcessStartInfo
            {
                FileName = "sudo",
                Arguments = $"\"{processPath}\" {invocationArgs}",
                UseShellExecute = false
            };
        }

        if (OperatingSystem.IsMacOS())
        {
            return new ProcessStartInfo
            {
                FileName = "sudo",
                Arguments = $"\"{processPath}\" {invocationArgs}",
                UseShellExecute = false
            };
        }

        return new ProcessStartInfo
        {
            FileName = processPath,
            Arguments = invocationArgs,
            UseShellExecute = true
        };
    }

    private static bool IsElevated()
    {
        if (OperatingSystem.IsWindows())
        {
            using var identity = WindowsIdentity.GetCurrent();
            var principal = new WindowsPrincipal(identity);
            return principal.IsInRole(WindowsBuiltInRole.Administrator);
        }

        if (OperatingSystem.IsLinux() || OperatingSystem.IsMacOS())
        {
            return string.Equals(Environment.UserName, "root", StringComparison.OrdinalIgnoreCase);
        }

        return false;
    }
}
