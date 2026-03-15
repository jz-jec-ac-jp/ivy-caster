using IvyCaster.Core;

namespace IvyCaster.Agent.Platform.Linux;

public sealed class LinuxAgentServiceController : IAgentServiceController
{
    public Task<AgentServiceState> GetStateAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(AgentServiceState.Unknown);
    }

    public Task<ManagementOperationResult> StopAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new ManagementOperationResult(false, "Stop is not implemented on this platform yet."));
    }

    public Task<ManagementOperationResult> RestartAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new ManagementOperationResult(false, "Restart is not implemented on this platform yet."));
    }
}
