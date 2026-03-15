using System.Diagnostics;
using IvyCaster.Core;

namespace IvyCaster.Agent.Platform;

public sealed class WindowsProcessRunner : IProcessRunner
{
    public async Task<CommandExecutionResult> ExecuteAsync(
        CommandExecutionRequest request,
        CancellationToken cancellationToken = default)
    {
        var startedAt = DateTimeOffset.UtcNow;

        var fileName = request.Shell switch
        {
            ShellKind.Cmd => "cmd.exe",
            ShellKind.PowerShell => "powershell.exe",
            _ => "cmd.exe"
        };

        var fullCommand = string.IsNullOrWhiteSpace(request.Arguments)
            ? request.Command
            : $"{request.Command} {request.Arguments}";

        var shellArguments = request.Shell switch
        {
            ShellKind.Cmd => $"/c \"{fullCommand}\"",
            ShellKind.PowerShell => $"-NoProfile -ExecutionPolicy Bypass -Command \"{fullCommand}\"",
            _ => $"/c \"{fullCommand}\""
        };

        var startInfo = new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = shellArguments,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        if (!string.IsNullOrWhiteSpace(request.WorkingDirectory))
        {
            startInfo.WorkingDirectory = request.WorkingDirectory;
        }

        using var process = new Process { StartInfo = startInfo };
        process.Start();

        var outputTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var errorTask = process.StandardError.ReadToEndAsync(cancellationToken);

        await process.WaitForExitAsync(cancellationToken);

        var output = await outputTask;
        var error = await errorTask;
        var finishedAt = DateTimeOffset.UtcNow;

        return new CommandExecutionResult(
            Success: process.ExitCode == 0,
            ExitCode: process.ExitCode,
            StandardOutput: output,
            StandardError: error,
            StartedAtUtc: startedAt,
            FinishedAtUtc: finishedAt);
    }
}
