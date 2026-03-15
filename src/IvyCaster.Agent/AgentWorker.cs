using System.Runtime.InteropServices;
using IvyCaster.Agent.Runtime;
using IvyCaster.Core;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace IvyCaster.Agent;

public sealed class AgentWorker(
    AgentRuntimeState runtimeState,
    IHeartbeatReporter heartbeatReporter,
    ILogger<AgentWorker> logger) : BackgroundService
{
    private readonly string _agentId = Environment.MachineName;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("IvyCaster Agent started. agentId={AgentId}", _agentId);

        while (!stoppingToken.IsCancellationRequested)
        {
            var payload = new HeartbeatPayload(
                AgentId: _agentId,
                HostName: Environment.MachineName,
                OsDescription: RuntimeInformation.OSDescription,
                TimestampUtc: DateTimeOffset.UtcNow
            );

            runtimeState.MarkHeartbeat(payload.TimestampUtc);
            await heartbeatReporter.ReportAsync(payload, stoppingToken);

            try
            {
                await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }
}
