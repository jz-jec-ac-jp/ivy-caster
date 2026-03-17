using IvyCaster.Agent.Manager.Services;
using Xunit;

namespace IvyCaster.Core.Tests;

public sealed class OperationElevationServiceTests
{
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("status")]
    [InlineData("restart-now")]
    public async Task ElevateAndExecuteAsync_UnsupportedOperation_ReturnsFailure(string operationName)
    {
        var sut = new OperationElevationService();

        var result = await sut.ElevateAndExecuteAsync(operationName);

        Assert.False(result.Success);
        Assert.False(result.WasCancelled);
        Assert.Equal("Unsupported operation. Supported operations: stop, restart.", result.Message);
    }
}
