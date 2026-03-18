using System.Reflection;
using IvyCaster.Agent.Platform.Windows;
using Xunit;

namespace IvyCaster.Agent.Tests;

[Collection("Non-Parallel")]
public sealed class WindowsAgentServiceControllerTests
{
    private const string ServiceNameEnv = "IVYCASTER_AGENT_SERVICE_NAME";

    [SkippableFact]
    public void ResolveServiceName_WhenEnvironmentVariableIsSet_ReturnsConfiguredValue()
    {
        Skip.IfNot(OperatingSystem.IsWindows(), "Windows-only test.");

        var original = Environment.GetEnvironmentVariable(ServiceNameEnv);
        const string expected = "ivycaster-agent-test-service";
        try
        {
            Environment.SetEnvironmentVariable(ServiceNameEnv, expected);

            var resolved = InvokeResolveServiceName();
            Assert.Equal(expected, resolved);
        }
        finally
        {
            Environment.SetEnvironmentVariable(ServiceNameEnv, original);
        }
    }

    [SkippableFact]
    public void ResolveServiceName_WhenEnvironmentVariableMissing_ThrowsInvalidOperationException()
    {
        Skip.IfNot(OperatingSystem.IsWindows(), "Windows-only test.");

        var original = Environment.GetEnvironmentVariable(ServiceNameEnv);
        try
        {
            Environment.SetEnvironmentVariable(ServiceNameEnv, null);

            try
            {
                var resolved = InvokeResolveServiceName();
                Skip.If(
                    !string.IsNullOrWhiteSpace(resolved),
                    "Registry fallback is configured on this machine; fail-fast path is not applicable.");
            }
            catch (TargetInvocationException ex) when (ex.InnerException is InvalidOperationException inner)
            {
                Assert.Contains("service name is not configured", inner.Message, StringComparison.OrdinalIgnoreCase);
                return;
            }

            throw new Xunit.Sdk.XunitException("Expected InvalidOperationException when service name is not configured.");
        }
        finally
        {
            Environment.SetEnvironmentVariable(ServiceNameEnv, original);
        }
    }

    private static string InvokeResolveServiceName()
    {
        var method = typeof(WindowsAgentServiceController).GetMethod(
            "ResolveServiceName",
            BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(method);
        return (string)method!.Invoke(null, null)!;
    }
}
