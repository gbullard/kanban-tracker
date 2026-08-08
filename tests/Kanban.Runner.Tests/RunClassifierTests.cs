using Kanban.Core.Enums;
using Kanban.Runner.Classification;
using Kanban.Runner.Contracts;
using Xunit;

namespace Kanban.Runner.Tests;

public class RunClassifierTests
{
    private static ResultFileRead Completed(string summary = "Did the thing.") =>
        ResultFileRead.Valid(new AgentResultFile(AgentStatus.Completed, summary, null));

    private static RunFacts Facts(
        string? gitFailure = null,
        bool timedOut = false,
        int exitCode = 0,
        ResultFileRead? result = null,
        bool commitProduced = true) =>
        new(gitFailure, timedOut, 20, exitCode, result ?? Completed(), commitProduced);

    [Fact]
    public void Git_preparation_failure_wins_over_everything_else()
    {
        var c = RunClassifier.Classify(Facts(gitFailure: "working tree not clean", exitCode: 0));

        Assert.Equal(RunOutcome.Failed, c.Outcome);
        Assert.Equal("working tree not clean", c.FailureReason);
    }

    [Fact]
    public void Timeout_is_a_failure_naming_the_limit()
    {
        var c = RunClassifier.Classify(Facts(timedOut: true, exitCode: -1));

        Assert.Equal(RunOutcome.Failed, c.Outcome);
        Assert.Equal("timed out after 20 minutes", c.FailureReason);
    }

    [Fact]
    public void A_blocked_result_is_a_failure_carrying_the_agents_reason()
    {
        var blocked = ResultFileRead.Valid(
            new AgentResultFile(AgentStatus.Blocked, "Got partway.", "No credentials."));

        var c = RunClassifier.Classify(Facts(result: blocked));

        Assert.Equal(RunOutcome.Failed, c.Outcome);
        Assert.Equal("No credentials.", c.FailureReason);
        Assert.Equal("Got partway.", c.Summary);
    }

    [Fact]
    public void A_blocked_result_with_no_reason_still_fails_with_a_usable_message()
    {
        var blocked = ResultFileRead.Valid(new AgentResultFile(AgentStatus.Blocked, "Tried.", null));

        var c = RunClassifier.Classify(Facts(result: blocked));

        Assert.Equal(RunOutcome.Failed, c.Outcome);
        Assert.Equal("agent reported it was blocked but gave no reason", c.FailureReason);
    }

    [Fact]
    public void A_missing_result_file_is_a_failure()
    {
        var c = RunClassifier.Classify(Facts(result: ResultFileRead.Missing()));

        Assert.Equal(RunOutcome.Failed, c.Outcome);
        Assert.Equal("agent produced no result file", c.FailureReason);
    }

    [Fact]
    public void A_malformed_result_file_is_a_failure()
    {
        var c = RunClassifier.Classify(Facts(result: ResultFileRead.Malformed()));

        Assert.Equal(RunOutcome.Failed, c.Outcome);
        Assert.Equal("result file could not be parsed", c.FailureReason);
    }

    [Fact]
    public void A_non_zero_exit_is_a_failure_even_with_a_completed_result()
    {
        var c = RunClassifier.Classify(Facts(exitCode: 3));

        Assert.Equal(RunOutcome.Failed, c.Outcome);
        Assert.Equal("agent exited with code 3", c.FailureReason);
    }

    [Fact]
    public void A_clean_run_with_a_commit_succeeds()
    {
        var c = RunClassifier.Classify(Facts());

        Assert.Equal(RunOutcome.Succeeded, c.Outcome);
        Assert.Null(c.FailureReason);
        Assert.Equal("Did the thing.", c.Summary);
    }

    [Fact]
    public void A_clean_run_that_changed_nothing_still_succeeds_and_says_so()
    {
        var c = RunClassifier.Classify(Facts(commitProduced: false, result: Completed("Already correct.")));

        Assert.Equal(RunOutcome.Succeeded, c.Outcome);
        Assert.Null(c.FailureReason);
        Assert.Contains("Already correct.", c.Summary);
        Assert.Contains("No files were changed", c.Summary);
    }

    [Fact]
    public void A_completed_result_with_no_summary_gets_a_placeholder_rather_than_null()
    {
        var c = RunClassifier.Classify(Facts(result: ResultFileRead.Valid(
            new AgentResultFile(AgentStatus.Completed, null, null))));

        Assert.Equal(RunOutcome.Succeeded, c.Outcome);
        Assert.False(string.IsNullOrWhiteSpace(c.Summary));
    }
}