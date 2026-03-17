using IvyCaster.Core;
using Xunit;

namespace IvyCaster.Agent.Tests;

public sealed class LinuxProcessRunnerTests
{
    [Fact]
    public void ShellKind_IncludesPwshAndBash()
    {
        Assert.Contains(ShellKind.Pwsh, Enum.GetValues<ShellKind>());
        Assert.Contains(ShellKind.Bash, Enum.GetValues<ShellKind>());
    }

    [Fact]
    public void CommandExecutionRequest_DefaultShell_IsCmdForCompatibility()
    {
        var request = new CommandExecutionRequest(Command: "echo");
        Assert.Equal(ShellKind.Cmd, request.Shell);
    }
}
