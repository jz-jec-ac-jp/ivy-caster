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
