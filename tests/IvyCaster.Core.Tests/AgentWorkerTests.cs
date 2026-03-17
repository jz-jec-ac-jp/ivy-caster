using IvyCaster.Agent;
using IvyCaster.Agent.Runtime;
using IvyCaster.Core;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace IvyCaster.Core.Tests;

public sealed class AgentWorkerTests
{
    [Fact]
    public async Task StartAsync_OnSuccessfulHeartbeat_UpdatesRuntimeState()
    {
        var runtimeState = new AgentRuntimeState("agent-1", "host-1");
        var reporter = new RecordingHeartbeatReporter(throwOnReport: false);
        var worker = new AgentWorker(runtimeState, reporter, NullLogger<AgentWorker>.Instance);

        await worker.StartAsync(CancellationToken.None);
        await reporter.FirstCall.Task.WaitAsync(TimeSpan.FromSeconds(2));
        await worker.StopAsync(CancellationToken.None);

        Assert.NotNull(reporter.LastPayload);
        Assert.Equal(reporter.LastPayload!.TimestampUtc, runtimeState.LastHeartbeatUtc);
    }

    [Fact]
    public async Task StartAsync_OnHeartbeatFailure_DoesNotUpdateRuntimeState()
    {
        var runtimeState = new AgentRuntimeState("agent-1", "host-1");
        var before = runtimeState.LastHeartbeatUtc;
        var reporter = new RecordingHeartbeatReporter(throwOnReport: true);
        var worker = new AgentWorker(runtimeState, reporter, NullLogger<AgentWorker>.Instance);

        await worker.StartAsync(CancellationToken.None);
        await reporter.FirstCall.Task.WaitAsync(TimeSpan.FromSeconds(2));
        await worker.StopAsync(CancellationToken.None);

        Assert.Equal(before, runtimeState.LastHeartbeatUtc);
    }

    private sealed class RecordingHeartbeatReporter(bool throwOnReport) : IHeartbeatReporter
    {
        public TaskCompletionSource<bool> FirstCall { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public HeartbeatPayload? LastPayload { get; private set; }

        public Task ReportAsync(HeartbeatPayload payload, CancellationToken cancellationToken = default)
        {
            LastPayload = payload;
            FirstCall.TrySetResult(true);
            if (throwOnReport)
            {
                throw new InvalidOperationException("Simulated heartbeat failure.");
            }

            return Task.CompletedTask;
        }
    }
}
