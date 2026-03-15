namespace IvyCaster.Agent.Runtime;

public sealed class AgentRuntimeState
{
    private readonly object _sync = new();
    private DateTimeOffset _lastHeartbeatUtc = DateTimeOffset.UtcNow;

    public AgentRuntimeState(string agentId, string hostName)
    {
        AgentId = agentId;
        HostName = hostName;
        StartedAtUtc = DateTimeOffset.UtcNow;
    }

    public string AgentId { get; }
    public string HostName { get; }
    public DateTimeOffset StartedAtUtc { get; }

    public DateTimeOffset LastHeartbeatUtc
    {
        get
        {
            lock (_sync)
            {
                return _lastHeartbeatUtc;
            }
        }
    }

    public void MarkHeartbeat(DateTimeOffset timestampUtc)
    {
        lock (_sync)
        {
            _lastHeartbeatUtc = timestampUtc;
        }
    }
}
