using IvyCaster.Core;

namespace IvyCaster.Agent.Platform.Linux;

public sealed class LinuxAgentLogProvider : IAgentLogProvider
{
    public Task<IReadOnlyList<AgentLogEntry>> GetRecentAsync(int take, CancellationToken cancellationToken = default)
    {
        IReadOnlyList<AgentLogEntry> result = Array.Empty<AgentLogEntry>();
        return Task.FromResult(result);
    }
}
