namespace Kanban.Runner.Contracts;

public enum AgentStatus
{
    Completed,
    Blocked
}

public enum ResultFileState
{
    Missing,
    Malformed,
    Valid
}

public record AgentResultFile(AgentStatus Status, string? Summary, string? BlockedReason);

public record ResultFileRead(ResultFileState State, AgentResultFile? Result)
{
    public static ResultFileRead Missing() => new(ResultFileState.Missing, null);
    public static ResultFileRead Malformed() => new(ResultFileState.Malformed, null);
    public static ResultFileRead Valid(AgentResultFile result) => new(ResultFileState.Valid, result);
}