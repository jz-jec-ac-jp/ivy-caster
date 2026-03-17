using System.Diagnostics;
using System.Text;
using IvyCaster.Core;

namespace IvyCaster.Agent.Platform;

public sealed class WindowsProcessRunner : IProcessRunner
{
    public async Task<CommandExecutionResult> ExecuteAsync(
        CommandExecutionRequest request,
        CancellationToken cancellationToken = default)
    {
        var startedAt = DateTimeOffset.UtcNow;

        var startInfo = new ProcessStartInfo
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        switch (request.Shell)
        {
            case ShellKind.Cmd:
            {
                var fullCommand = BuildFullCommand(request.Command, request.Arguments);
                startInfo.FileName = "cmd.exe";
                startInfo.Arguments = $"/s /c {WrapForCmd(fullCommand)}";
                break;
            }
            case ShellKind.PowerShell:
            {
                var fullCommand = BuildFullCommand(request.Command, request.Arguments);
                var encodedCommand = Convert.ToBase64String(Encoding.Unicode.GetBytes(fullCommand));
                startInfo.FileName = "powershell.exe";
                startInfo.ArgumentList.Add("-NoProfile");
                startInfo.ArgumentList.Add("-ExecutionPolicy");
                startInfo.ArgumentList.Add("Bypass");
                startInfo.ArgumentList.Add("-EncodedCommand");
                startInfo.ArgumentList.Add(encodedCommand);
                break;
            }
            default:
                startInfo.FileName = request.Command;
                if (!string.IsNullOrWhiteSpace(request.Arguments))
                {
                    startInfo.Arguments = request.Arguments;
                }
                break;
        }

        if (!string.IsNullOrWhiteSpace(request.WorkingDirectory))
        {
            startInfo.WorkingDirectory = request.WorkingDirectory;
        }

        using var process = new Process { StartInfo = startInfo };
        process.Start();

        var outputTask = process.StandardOutput.ReadToEndAsync();
        var errorTask = process.StandardError.ReadToEndAsync();

        try
        {
            await process.WaitForExitAsync(cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            TerminateProcessTree(process);
            await Task.WhenAll(outputTask, errorTask);
            throw;
        }

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

    private static string BuildFullCommand(string command, string? arguments)
        => string.IsNullOrWhiteSpace(arguments) ? command : $"{command} {arguments}";

    private static string WrapForCmd(string command)
    {
        var escaped = command.Replace("\"", "\"\"");
        return $"\"{escaped}\"";
    }

    private static void TerminateProcessTree(Process process)
    {
        try
        {
            process.Kill(entireProcessTree: true);
        }
        catch (Exception ex) when (ex is InvalidOperationException or NotSupportedException or PlatformNotSupportedException)
        {
            try
            {
                process.Kill();
            }
            catch (Exception killEx) when (killEx is InvalidOperationException or NotSupportedException)
            {
                Trace.TraceWarning($"Failed to terminate process during cancellation: {killEx.Message}");
            }

            Trace.TraceWarning($"Failed to terminate process tree during cancellation: {ex.Message}");
        }

        try
        {
            process.WaitForExit();
        }
        catch (Exception ex) when (ex is InvalidOperationException)
        {
            Trace.TraceWarning($"Failed while waiting for process exit after cancellation: {ex.Message}");
        }
    }
}
