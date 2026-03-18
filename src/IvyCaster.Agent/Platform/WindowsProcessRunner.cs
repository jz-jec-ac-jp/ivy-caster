using System.Diagnostics;
using System.ComponentModel;
using System.Text;
using IvyCaster.Core;

namespace IvyCaster.Agent.Platform;

public sealed class WindowsProcessRunner : IProcessRunner
{
    private const int CancellationExitWaitTimeoutMs = 5000;
    private const int CancellationDrainTimeoutMs = 5000;

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
                startInfo.FileName = "powershell.exe";
                AddEncodedPowerShellCommandArguments(startInfo, request.Command, request.Arguments);
                break;
            }
            case ShellKind.Pwsh:
            {
                startInfo.FileName = "pwsh.exe";
                AddEncodedPowerShellCommandArguments(startInfo, request.Command, request.Arguments);
                break;
            }
            case ShellKind.Bash:
                throw new NotSupportedException("WindowsProcessRunner does not support shell kind: Bash.");
            default:
                throw new NotSupportedException($"WindowsProcessRunner does not support shell kind: {request.Shell}.");
        }

        if (!string.IsNullOrWhiteSpace(request.WorkingDirectory))
        {
            startInfo.WorkingDirectory = request.WorkingDirectory;
        }

        using var process = new Process { StartInfo = startInfo };
        cancellationToken.ThrowIfCancellationRequested();
        process.Start();

        var outputTask = process.StandardOutput.ReadToEndAsync();
        var errorTask = process.StandardError.ReadToEndAsync();

        try
        {
            await process.WaitForExitAsync(cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            var exited = TerminateProcessTree(process);
            if (!exited)
            {
                Trace.TraceWarning("Process did not fully terminate during cancellation; attempting bounded output drain.");
            }

            await DrainStreamsWithTimeoutAsync(outputTask, errorTask, CancellationDrainTimeoutMs);
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

    private static void AddEncodedPowerShellCommandArguments(ProcessStartInfo startInfo, string command, string? arguments)
    {
        var fullCommand = BuildFullCommand(command, arguments);
        var encodedCommand = Convert.ToBase64String(Encoding.Unicode.GetBytes(fullCommand));
        startInfo.ArgumentList.Add("-NoProfile");
        startInfo.ArgumentList.Add("-ExecutionPolicy");
        startInfo.ArgumentList.Add("Bypass");
        startInfo.ArgumentList.Add("-EncodedCommand");
        startInfo.ArgumentList.Add(encodedCommand);
    }

    private static string WrapForCmd(string command)
    {
        var escaped = command.Replace("\"", "\"\"");
        return $"\"{escaped}\"";
    }

    private static bool TerminateProcessTree(Process process)
    {
        try
        {
            process.Kill(entireProcessTree: true);
        }
        catch (Exception ex) when (ex is InvalidOperationException
            or NotSupportedException
            or PlatformNotSupportedException
            or Win32Exception
            or AggregateException)
        {
            try
            {
                process.Kill();
            }
            catch (Exception killEx) when (killEx is InvalidOperationException
                or NotSupportedException
                or Win32Exception
                or AggregateException)
            {
                Trace.TraceWarning($"Failed to terminate process during cancellation: {killEx}");
            }

            Trace.TraceWarning($"Failed to terminate process tree during cancellation: {ex}");
        }

        try
        {
            if (!process.WaitForExit(CancellationExitWaitTimeoutMs))
            {
                Trace.TraceWarning(
                    $"Timed out waiting for process exit after cancellation. timeoutMs={CancellationExitWaitTimeoutMs}, processId={process.Id}");
                return false;
            }

            return true;
        }
        catch (Exception ex) when (ex is InvalidOperationException)
        {
            Trace.TraceWarning($"Failed while waiting for process exit after cancellation: {ex}");
            return false;
        }
    }

    private static async Task DrainStreamsWithTimeoutAsync(Task outputTask, Task errorTask, int timeoutMs)
    {
        var drainTask = Task.WhenAll(outputTask, errorTask);
        var completedTask = await Task.WhenAny(drainTask, Task.Delay(timeoutMs));
        if (completedTask != drainTask)
        {
            Trace.TraceWarning($"Timed out draining process output after cancellation. timeoutMs={timeoutMs}");
            return;
        }

        try
        {
            await drainTask;
        }
        catch (Exception ex)
        {
            Trace.TraceWarning($"Failed while draining process output after cancellation: {ex}");
        }
    }
}
