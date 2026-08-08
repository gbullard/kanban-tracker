namespace Kanban.Runner.Agents;

public record AgentRequest(string WorkingDirectory, string Prompt);

public record AgentExecution(int ExitCode, bool TimedOut);