using IvyCaster.Core;

namespace IvyCaster.Agent.Platform.Linux;

public sealed class LinuxAgentProcessInspector : IAgentProcessInspector
{
    public Task<AgentProcessSnapshot> GetSnapshotAsync(CancellationToken cancellationToken = default)
    {
        var snapshot = new AgentProcessSnapshot(
            ProcessId: Environment.ProcessId,
            ProcessName: "IvyCaster.Agent",
            StartedAtUtc: DateTimeOffset.UtcNow,
            LastHeartbeatUtc: DateTimeOffset.UtcNow);

        return Task.FromResult(snapshot);
    }
}
