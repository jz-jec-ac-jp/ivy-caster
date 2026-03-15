using System.Diagnostics;
using IvyCaster.Agent.Runtime;
using IvyCaster.Core;

namespace IvyCaster.Agent.Platform.Windows;

public sealed class WindowsAgentProcessInspector(AgentRuntimeState runtimeState) : IAgentProcessInspector
{
    public Task<AgentProcessSnapshot> GetSnapshotAsync(CancellationToken cancellationToken = default)
    {
        var process = Process.GetCurrentProcess();
        var snapshot = new AgentProcessSnapshot(
            ProcessId: process.Id,
            ProcessName: process.ProcessName,
            StartedAtUtc: runtimeState.StartedAtUtc,
            LastHeartbeatUtc: runtimeState.LastHeartbeatUtc);

        return Task.FromResult(snapshot);
    }
}
