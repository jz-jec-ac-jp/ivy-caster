using IvyCaster.Agent.Runtime;
using IvyCaster.Core;

namespace IvyCaster.Agent.Platform.Windows;

public sealed class WindowsAgentLogProvider(IAgentRuntimeLogStore logStore) : IAgentLogProvider
{
    public Task<IReadOnlyList<AgentLogEntry>> GetRecentAsync(int take, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(logStore.GetRecent(take));
    }
}
