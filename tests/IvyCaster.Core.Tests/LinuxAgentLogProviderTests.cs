using IvyCaster.Agent.Platform.Linux;
using Xunit;

namespace IvyCaster.Core.Tests;

public sealed class LinuxAgentLogProviderTests
{
    [Fact]
    public async Task GetRecentAsync_ReturnsEmptyCollection()
    {
        var sut = new LinuxAgentLogProvider();

        var result = await sut.GetRecentAsync(100);

        Assert.NotNull(result);
        Assert.Empty(result);
    }
}
