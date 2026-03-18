using IvyCaster.Core;

namespace IvyCaster.Infrastructure;

public sealed class WmiDeploymentChannel : IDeploymentChannel
{
    public string ChannelName => "WMI";

    public Task<DeploymentResult> DeployAsync(DeploymentRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.TargetHost))
        {
            return Task.FromResult(new DeploymentResult(false, "Target host is required."));
        }

        if (string.IsNullOrWhiteSpace(request.PackagePath))
        {
            return Task.FromResult(new DeploymentResult(false, "Package path is required."));
        }

        // Minimal scaffold: actual WMI deployment flow will be implemented later.
        var message = $"WMI deployment scaffold ready. target={request.TargetHost}, package={request.PackagePath}";
        return Task.FromResult(new DeploymentResult(true, message));
    }
}
