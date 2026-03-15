namespace IvyCaster.Core;

public interface IDeploymentChannel
{
    string ChannelName { get; }
    Task<DeploymentResult> DeployAsync(DeploymentRequest request, CancellationToken cancellationToken = default);
}

public sealed record DeploymentRequest(
    string TargetHost,
    string PackagePath,
    string? Arguments = null
);

public sealed record DeploymentResult(
    bool Success,
    string Message
);

public enum ShellKind
{
    Cmd,
    PowerShell
}

public sealed record CommandExecutionRequest(
    string Command,
    string? Arguments = null,
    string? WorkingDirectory = null,
    ShellKind Shell = ShellKind.Cmd
);

public sealed record CommandExecutionResult(
    bool Success,
    int ExitCode,
    string StandardOutput,
    string StandardError,
    DateTimeOffset StartedAtUtc,
    DateTimeOffset FinishedAtUtc
);

public interface IProcessRunner
{
    Task<CommandExecutionResult> ExecuteAsync(
        CommandExecutionRequest request,
        CancellationToken cancellationToken = default);
}

public sealed record HeartbeatPayload(
    string AgentId,
    string HostName,
    string OsDescription,
    DateTimeOffset TimestampUtc
);

public interface IHeartbeatReporter
{
    Task ReportAsync(HeartbeatPayload payload, CancellationToken cancellationToken = default);
}

public enum AgentServiceState
{
    Unknown,
    Running,
    Stopped,
    Error
}

public sealed record AgentProcessSnapshot(
    int ProcessId,
    string ProcessName,
    DateTimeOffset StartedAtUtc,
    DateTimeOffset LastHeartbeatUtc
);

public sealed record AgentStatusSnapshot(
    string AgentId,
    string HostName,
    AgentServiceState State,
    DateTimeOffset CheckedAtUtc,
    AgentProcessSnapshot Process
);

public sealed record AgentLogEntry(
    DateTimeOffset TimestampUtc,
    string Level,
    string Message
);

public sealed record ManagementOperationResult(
    bool Success,
    string Message
);

public interface IAgentServiceController
{
    Task<AgentServiceState> GetStateAsync(CancellationToken cancellationToken = default);
    Task<ManagementOperationResult> StopAsync(CancellationToken cancellationToken = default);
    Task<ManagementOperationResult> RestartAsync(CancellationToken cancellationToken = default);
}

public interface IAgentLogProvider
{
    Task<IReadOnlyList<AgentLogEntry>> GetRecentAsync(int take, CancellationToken cancellationToken = default);
}

public interface IAgentProcessInspector
{
    Task<AgentProcessSnapshot> GetSnapshotAsync(CancellationToken cancellationToken = default);
}

public static class AgentManagementDefaults
{
    public const string PipeName = "ivy-caster-agent-management-v1";
}

public sealed record AgentManagementRequest(
    string Action,
    int? Take = null
);

public sealed record AgentManagementResponse(
    bool Success,
    string Message,
    AgentStatusSnapshot? Status = null,
    IReadOnlyList<AgentLogEntry>? Logs = null
);

public interface IAgentManagementService
{
    Task<AgentManagementResponse> HandleAsync(
        AgentManagementRequest request,
        CancellationToken cancellationToken = default);
}

public interface IPrivilegeElevationService
{
    Task<ElevationResult> ElevateAndExecuteAsync(
        string operationName,
        CancellationToken cancellationToken = default);
}

public sealed record ElevationResult(
    bool Success,
    bool WasCancelled,
    string Message
);
