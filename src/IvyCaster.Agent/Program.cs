using IvyCaster.Core;
using IvyCaster.Infrastructure;

IDeploymentChannel deploymentChannel = new WmiDeploymentChannel();

var request = new DeploymentRequest(
    TargetHost: "localhost",
    PackagePath: "./agent-package.msi",
    Arguments: "/quiet"
);

var result = await deploymentChannel.DeployAsync(request);
Console.WriteLine($"[{deploymentChannel.ChannelName}] success={result.Success} message=\"{result.Message}\"");
