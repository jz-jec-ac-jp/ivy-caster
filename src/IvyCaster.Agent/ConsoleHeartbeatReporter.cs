using IvyCaster.Core;
using Microsoft.Extensions.Logging;

namespace IvyCaster.Agent;

public sealed class ConsoleHeartbeatReporter(ILogger<ConsoleHeartbeatReporter> logger) : IHeartbeatReporter
{
    public Task ReportAsync(HeartbeatPayload payload, CancellationToken cancellationToken = default)
    {
        logger.LogInformation(
            "Heartbeat sent. agentId={AgentId}, host={HostName}, os={OsDescription}, timestamp={TimestampUtc}",
            payload.AgentId,
            payload.HostName,
            payload.OsDescription,
            payload.TimestampUtc);

        return Task.CompletedTask;
    }
}
