using System.Diagnostics;
using System.ComponentModel;
using System.Text;
using IvyCaster.Core;

namespace IvyCaster.Agent.Platform.Linux;

public sealed class LinuxProcessRunner : IProcessRunner
{
    public async Task<CommandExecutionResult> ExecuteAsync(
        CommandExecutionRequest request,
        CancellationToken cancellationToken = default)
    {
        var startedAt = DateTimeOffset.UtcNow;
        var startInfo = BuildStartInfo(request);

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

    private static ProcessStartInfo BuildStartInfo(CommandExecutionRequest request)
    {
        var startInfo = new ProcessStartInfo
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        var shell = request.Shell;
        if (shell == ShellKind.Cmd)
        {
            Trace.TraceWarning("ShellKind.Cmd on Linux is deprecated; using Bash execution for compatibility.");
            shell = ShellKind.Bash;
        }
        else if (shell == ShellKind.PowerShell)
        {
            Trace.TraceWarning("ShellKind.PowerShell on Linux is deprecated; using Pwsh execution for compatibility.");
            shell = ShellKind.Pwsh;
        }

        switch (shell)
        {
            case ShellKind.Bash:
                startInfo.FileName = "/bin/bash";
                startInfo.ArgumentList.Add("-lc");
                startInfo.ArgumentList.Add(BuildFullCommand(request.Command, request.Arguments));
                break;
            case ShellKind.Pwsh:
                startInfo.FileName = "pwsh";
                AddEncodedPowerShellCommandArguments(startInfo, request.Command, request.Arguments);
                break;
            default:
                throw new NotSupportedException($"LinuxProcessRunner does not support shell kind: {request.Shell}.");
        }

        if (!string.IsNullOrWhiteSpace(request.WorkingDirectory))
        {
            startInfo.WorkingDirectory = request.WorkingDirectory;
        }

        return startInfo;
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
        catch (Exception ex) when (ex is PlatformNotSupportedException
            or NotSupportedException
            or Win32Exception
            or AggregateException)
        {
            Trace.TraceWarning($"Failed to kill process tree directly; falling back to SIGTERM path: {ex}");
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
