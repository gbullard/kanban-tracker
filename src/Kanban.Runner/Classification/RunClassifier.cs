using Kanban.Core.Enums;
using Kanban.Runner.Contracts;

namespace Kanban.Runner.Classification;

public record RunFacts(
    string? GitFailure,
    bool TimedOut,
    int TimeoutMinutes,
    int ExitCode,
    ResultFileRead Result,
    bool CommitProduced);

public record Classification(RunOutcome Outcome, string? FailureReason, string? Summary);

/// <summary>
/// Turns the observable facts of a run into the outcome shown on the board. Rules are
/// evaluated in order and the first match wins.
/// </summary>
public static class RunClassifier
{
    public static Classification Classify(RunFacts facts)
    {
        if (facts.GitFailure is not null)
        {
            return new Classification(RunOutcome.Failed, facts.GitFailure, null);
        }

        if (facts.TimedOut)
        {
            return new Classification(
                RunOutcome.Failed,
                $"timed out after {facts.TimeoutMinutes} minutes",
                facts.Result.Result?.Summary);
        }

        switch (facts.Result.State)
        {
            case ResultFileState.Missing:
                return new Classification(RunOutcome.Failed, "agent produced no result file", null);

            case ResultFileState.Malformed:
                return new Classification(RunOutcome.Failed, "result file could not be parsed", null);
        }

        var result = facts.Result.Result!;

        if (result.Status == AgentStatus.Blocked)
        {
            var reason = string.IsNullOrWhiteSpace(result.BlockedReason)
                ? "agent reported it was blocked but gave no reason"
                : result.BlockedReason;

            return new Classification(RunOutcome.Failed, reason, result.Summary);
        }

        if (facts.ExitCode != 0)
        {
            return new Classification(
                RunOutcome.Failed,
                $"agent exited with code {facts.ExitCode}",
                result.Summary);
        }

        var summary = string.IsNullOrWhiteSpace(result.Summary)
            ? "The agent reported completion but gave no summary."
            : result.Summary.Trim();

        if (!facts.CommitProduced)
        {
            summary += "\n\nNo files were changed, so nothing was committed.";
        }

        return new Classification(RunOutcome.Succeeded, null, summary);
    }
}