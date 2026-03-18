using IvyCaster.Agent.Platform;
using IvyCaster.Core;
using Xunit;

namespace IvyCaster.Agent.Tests;

public sealed class WindowsProcessRunnerTests
{
    [SkippableFact]
    public async Task ExecuteAsync_Cmd_EchoesExpectedOutput()
    {
        Skip.IfNot(OperatingSystem.IsWindows(), "Windows-only test.");

        var sut = new WindowsProcessRunner();
        var request = new CommandExecutionRequest(
            Command: "echo",
            Arguments: "hello-from-cmd",
            Shell: ShellKind.Cmd);

        var result = await sut.ExecuteAsync(request);

        Assert.True(result.Success);
        Assert.Equal(0, result.ExitCode);
        Assert.Contains("hello-from-cmd", result.StandardOutput, StringComparison.OrdinalIgnoreCase);
    }

    [SkippableFact]
    public async Task ExecuteAsync_PowerShell_UsesEncodedCommand()
    {
        Skip.IfNot(OperatingSystem.IsWindows(), "Windows-only test.");

        var sut = new WindowsProcessRunner();
        var request = new CommandExecutionRequest(
            Command: "Write-Output \"hello from powershell\"",
            Shell: ShellKind.PowerShell);

        var result = await sut.ExecuteAsync(request);

        Assert.True(result.Success);
        Assert.Equal(0, result.ExitCode);
        Assert.Contains("hello from powershell", result.StandardOutput, StringComparison.OrdinalIgnoreCase);
    }

    [SkippableFact]
    public async Task ExecuteAsync_WhenCancelled_ThrowsOperationCanceledException()
    {
        Skip.IfNot(OperatingSystem.IsWindows(), "Windows-only test.");

        var sut = new WindowsProcessRunner();
        var request = new CommandExecutionRequest(
            Command: "Start-Sleep -Seconds 10",
            Shell: ShellKind.PowerShell);
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(300));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => sut.ExecuteAsync(request, cts.Token));
    }

    [SkippableFact]
    public async Task ExecuteAsync_Bash_ThrowsNotSupportedException()
    {
        Skip.IfNot(OperatingSystem.IsWindows(), "Windows-only test.");

        var sut = new WindowsProcessRunner();
        var request = new CommandExecutionRequest(
            Command: "echo hello",
            Shell: ShellKind.Bash);

        var ex = await Assert.ThrowsAsync<NotSupportedException>(() => sut.ExecuteAsync(request));
        Assert.Contains(nameof(ShellKind.Bash), ex.Message, StringComparison.Ordinal);
    }
}
