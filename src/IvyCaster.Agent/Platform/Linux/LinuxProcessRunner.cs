using System.Diagnostics;
using IvyCaster.Core;

namespace IvyCaster.Agent.Platform.Linux;

public sealed class LinuxProcessRunner : IProcessRunner
{
    public async Task<CommandExecutionResult> ExecuteAsync(
        CommandExecutionRequest request,
        CancellationToken cancellationToken = default)
    {
        var startedAt = DateTimeOffset.UtcNow;

        var startInfo = new ProcessStartInfo
        {
            FileName = request.Command,
            Arguments = request.Arguments,
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
        using var cancellationRegistration = cancellationToken.Register(() => TerminateProcessTree(process));

        var outputTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var errorTask = process.StandardError.ReadToEndAsync(cancellationToken);
        var waitForExitTask = process.WaitForExitAsync(cancellationToken);

        await Task.WhenAll(waitForExitTask, outputTask, errorTask);

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

    private static void TerminateProcessTree(Process process)
    {
        try
        {
            if (process.HasExited)
            {
                return;
            }
        }
        catch (InvalidOperationException)
        {
            return;
        }

        try
        {
            process.Kill(entireProcessTree: true);
            return;
        }
        catch (Exception ex) when (ex is PlatformNotSupportedException or NotSupportedException)
        {
            // Fall back to SIGTERM + forced kill if tree kill is unavailable.
        }
        catch (InvalidOperationException)
        {
            return;
        }

        TrySendSigTerm(process.Id);

        try
        {
            if (!process.WaitForExit(1000))
            {
                process.Kill();
            }
        }
        catch (InvalidOperationException)
        {
        }
    }

    private static void TrySendSigTerm(int processId)
    {
        try
        {
            using var killProcess = Process.Start(new ProcessStartInfo
            {
                FileName = "/bin/kill",
                Arguments = $"-TERM {processId}",
                UseShellExecute = false,
                CreateNoWindow = true
            });
            killProcess?.WaitForExit(1000);
        }
        catch (Exception)
        {
        }
    }
}
