using Kanban.Core.Enums;

namespace Kanban.Runner.Agents;

/// <summary>
/// The only place the AI agent is launched. Everything else in the Runner treats the agent
/// as a function from a prompt and a directory to an exit code plus a stream of log lines.
/// </summary>
public interface IAgentProcess
{
    Task<AgentExecution> RunAsync(
        AgentRequest request,
        Func<LogStream, string, Task> onLine,
        CancellationToken ct);
}