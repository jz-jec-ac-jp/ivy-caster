using IvyCaster.Agent.Runtime;
using IvyCaster.Core;

namespace IvyCaster.Agent.Management;

public sealed class AgentManagementService(
    AgentRuntimeState runtimeState,
    IAgentServiceController serviceController,
    IAgentLogProvider logProvider,
    IAgentProcessInspector processInspector) : IAgentManagementService
{
    public async Task<AgentManagementResponse> HandleAsync(
        AgentManagementRequest request,
        CancellationToken cancellationToken = default)
    {
        var action = (request.Action ?? string.Empty).Trim().ToLowerInvariant();

        return action switch
        {
            "status" => await GetStatusResponseAsync(cancellationToken),
            "logs" => await GetLogsResponseAsync(request.Take ?? 100, cancellationToken),
            "stop" => await StopResponseAsync(cancellationToken),
            "restart" => await RestartResponseAsync(cancellationToken),
            _ => new AgentManagementResponse(false, $"Unknown action: {request.Action}")
        };
    }

    private async Task<AgentManagementResponse> GetStatusResponseAsync(CancellationToken cancellationToken)
    {
        var state = await serviceController.GetStateAsync(cancellationToken);
        var process = await processInspector.GetSnapshotAsync(cancellationToken);
        var status = new AgentStatusSnapshot(
            AgentId: runtimeState.AgentId,
            HostName: runtimeState.HostName,
            State: state,
            CheckedAtUtc: DateTimeOffset.UtcNow,
            Process: process);

        return new AgentManagementResponse(true, "Status fetched.", Status: status);
    }

    private async Task<AgentManagementResponse> GetLogsResponseAsync(int take, CancellationToken cancellationToken)
    {
        var logs = await logProvider.GetRecentAsync(take, cancellationToken);
        return new AgentManagementResponse(true, "Logs fetched.", Logs: logs);
    }

    private async Task<AgentManagementResponse> StopResponseAsync(CancellationToken cancellationToken)
    {
        var result = await serviceController.StopAsync(cancellationToken);
        return new AgentManagementResponse(result.Success, result.Message);
    }

    private async Task<AgentManagementResponse> RestartResponseAsync(CancellationToken cancellationToken)
    {
        var result = await serviceController.RestartAsync(cancellationToken);
        return new AgentManagementResponse(result.Success, result.Message);
    }
}
