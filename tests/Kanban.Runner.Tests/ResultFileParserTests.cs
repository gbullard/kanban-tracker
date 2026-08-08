using Kanban.Runner.Contracts;
using Xunit;

namespace Kanban.Runner.Tests;

public class ResultFileParserTests
{
    [Fact]
    public void Parses_a_completed_result()
    {
        var read = ResultFileParser.Parse("""{"status":"completed","summary":"Added the login page."}""");

        Assert.Equal(ResultFileState.Valid, read.State);
        Assert.Equal(AgentStatus.Completed, read.Result!.Status);
        Assert.Equal("Added the login page.", read.Result.Summary);
        Assert.Null(read.Result.BlockedReason);
    }

    [Fact]
    public void Parses_a_blocked_result()
    {
        var read = ResultFileParser.Parse(
            """{"status":"blocked","summary":"Got partway.","blockedReason":"No database credentials."}""");

        Assert.Equal(ResultFileState.Valid, read.State);
        Assert.Equal(AgentStatus.Blocked, read.Result!.Status);
        Assert.Equal("No database credentials.", read.Result.BlockedReason);
    }

    [Fact]
    public void Status_is_matched_case_insensitively()
    {
        var read = ResultFileParser.Parse("""{"status":"COMPLETED","summary":"Done."}""");

        Assert.Equal(ResultFileState.Valid, read.State);
        Assert.Equal(AgentStatus.Completed, read.Result!.Status);
    }

    [Fact]
    public void Ignores_extra_properties_the_agent_invents()
    {
        var read = ResultFileParser.Parse(
            """{"status":"completed","summary":"Done.","filesTouched":["a.cs"],"confidence":0.9}""");

        Assert.Equal(ResultFileState.Valid, read.State);
        Assert.Equal("Done.", read.Result!.Summary);
    }

    [Fact]
    public void Null_content_means_the_file_was_missing()
    {
        Assert.Equal(ResultFileState.Missing, ResultFileParser.Parse(null).State);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Blank_content_is_treated_as_missing(string json)
    {
        Assert.Equal(ResultFileState.Missing, ResultFileParser.Parse(json).State);
    }

    [Theory]
    [InlineData("not json at all")]
    [InlineData("{\"status\":\"completed\"")]
    [InlineData("[]")]
    public void Unparseable_content_is_malformed(string json)
    {
        Assert.Equal(ResultFileState.Malformed, ResultFileParser.Parse(json).State);
    }

    [Fact]
    public void An_unrecognised_status_is_malformed()
    {
        Assert.Equal(ResultFileState.Malformed,
            ResultFileParser.Parse("""{"status":"finished","summary":"Done."}""").State);
    }

    [Fact]
    public void A_missing_status_is_malformed()
    {
        Assert.Equal(ResultFileState.Malformed,
            ResultFileParser.Parse("""{"summary":"Done."}""").State);
    }
}