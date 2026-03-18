using IvyCaster.Core;

namespace IvyCaster.Agent.Runtime;

public interface IAgentRuntimeLogStore
{
    void Add(string level, string message);
    IReadOnlyList<AgentLogEntry> GetRecent(int take);
}
