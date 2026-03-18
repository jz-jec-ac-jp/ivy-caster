using System.Diagnostics;
using IvyCaster.Agent.Platform.Linux;
using IvyCaster.Agent.Runtime;
using Xunit;

namespace IvyCaster.Agent.Tests;

public sealed class LinuxAgentProcessInspectorTests
{
    [Fact]
    public async Task GetSnapshotAsync_UsesRuntimeStateAndCurrentProcess()
    {
        var runtimeState = new AgentRuntimeState("agent-1", "host-1");
        var heartbeat = DateTimeOffset.UtcNow.AddMinutes(-3);
        runtimeState.MarkHeartbeat(heartbeat);
        var sut = new LinuxAgentProcessInspector(runtimeState);
        using var currentProcess = Process.GetCurrentProcess();

        var snapshot = await sut.GetSnapshotAsync();

        Assert.Equal(currentProcess.Id, snapshot.ProcessId);
        Assert.Equal(currentProcess.ProcessName, snapshot.ProcessName);
        Assert.Equal(runtimeState.StartedAtUtc, snapshot.StartedAtUtc);
        Assert.Equal(runtimeState.LastHeartbeatUtc, snapshot.LastHeartbeatUtc);
    }
}
