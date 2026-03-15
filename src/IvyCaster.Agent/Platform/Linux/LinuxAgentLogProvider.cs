using IvyCaster.Core;

namespace IvyCaster.Agent.Platform.Linux;

public sealed class LinuxAgentLogProvider : IAgentLogProvider
{
    public Task<IReadOnlyList<AgentLogEntry>> GetRecentAsync(int take, CancellationToken cancellationToken = default)
    {
        IReadOnlyList<AgentLogEntry> result =
        [
            new(DateTimeOffset.UtcNow, "Warning", "Linux log provider is not implemented yet.")
        ];

        return Task.FromResult(result);
    }
}
