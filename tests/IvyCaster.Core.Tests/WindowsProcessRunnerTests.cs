using IvyCaster.Agent.Platform;
using IvyCaster.Core;
using Xunit;

namespace IvyCaster.Core.Tests;

public sealed class WindowsProcessRunnerTests
{
    [Fact]
    public async Task ExecuteAsync_Cmd_EchoesExpectedOutput()
    {
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

    [Fact]
    public async Task ExecuteAsync_PowerShell_UsesEncodedCommand()
    {
        var sut = new WindowsProcessRunner();
        var request = new CommandExecutionRequest(
            Command: "Write-Output \"hello from powershell\"",
            Shell: ShellKind.PowerShell);

        var result = await sut.ExecuteAsync(request);

        Assert.True(result.Success);
        Assert.Equal(0, result.ExitCode);
        Assert.Contains("hello from powershell", result.StandardOutput, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExecuteAsync_WhenCancelled_ThrowsOperationCanceledException()
    {
        var sut = new WindowsProcessRunner();
        var request = new CommandExecutionRequest(
            Command: "Start-Sleep -Seconds 10",
            Shell: ShellKind.PowerShell);
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(300));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => sut.ExecuteAsync(request, cts.Token));
    }
}
