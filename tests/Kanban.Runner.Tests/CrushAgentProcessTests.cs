using Kanban.Core.Enums;
using Kanban.Runner.Agents;
using Kanban.Runner.Options;
using Microsoft.Extensions.Options;
using Xunit;

namespace Kanban.Runner.Tests;

public class CrushAgentProcessTests : IDisposable
{
    private readonly string _dir;

    public CrushAgentProcessTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "kanban-agent-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_dir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); } catch { /* best effort */ }
    }

    private static CrushAgentProcess Create(string arguments, int timeoutMinutes = 5)
    {
        var options = new RunnerOptions
        {
            AgentCommand = "cmd.exe",
            AgentArgumentTemplate = arguments,
            AgentTimeoutMinutes = timeoutMinutes,
            PromptDirectory = Path.Combine(Path.GetTempPath(), "kanban-prompts")
        };

        return new CrushAgentProcess(Microsoft.Extensions.Options.Options.Create(options));
    }

    [Fact]
    public async Task Captures_stdout_lines_in_order()
    {
        var lines = new List<string>();
        var agent = Create("/c echo one& echo two& echo three");

        var execution = await agent.RunAsync(
            new AgentRequest(_dir, "unused"),
            (_, text) => { lines.Add(text); return Task.CompletedTask; },
            default);

        Assert.Equal(0, execution.ExitCode);
        Assert.False(execution.TimedOut);
        Assert.Equal(new[] { "one", "two", "three" }, lines);
    }

    [Fact]
    public async Task Tags_stderr_separately_from_stdout()
    {
        var streams = new List<LogStream>();
        var agent = Create("/c echo out& echo err 1>&2");

        await agent.RunAsync(
            new AgentRequest(_dir, "unused"),
            (stream, _) => { streams.Add(stream); return Task.CompletedTask; },
            default);

        Assert.Contains(LogStream.Stdout, streams);
        Assert.Contains(LogStream.Stderr, streams);
    }

    [Fact]
    public async Task Reports_a_non_zero_exit_code()
    {
        var agent = Create("/c exit 7");

        var execution = await agent.RunAsync(
            new AgentRequest(_dir, "unused"),
            (_, _) => Task.CompletedTask,
            default);

        Assert.Equal(7, execution.ExitCode);
        Assert.False(execution.TimedOut);
    }

    [Fact]
    public async Task Runs_in_the_requested_working_directory()
    {
        var lines = new List<string>();
        var agent = Create("/c cd");

        await agent.RunAsync(
            new AgentRequest(_dir, "unused"),
            (_, text) => { lines.Add(text); return Task.CompletedTask; },
            default);

        Assert.Contains(lines, l => l.Trim().Equals(_dir, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Kills_the_process_and_reports_a_timeout()
    {
        // timeout /t waits; with a 0-minute limit the kill path runs immediately.
        var agent = Create("/c timeout /t 30 /nobreak", timeoutMinutes: 0);

        var execution = await agent.RunAsync(
            new AgentRequest(_dir, "unused"),
            (_, _) => Task.CompletedTask,
            default);

        Assert.True(execution.TimedOut);
    }

    [Fact]
    public async Task Writes_the_prompt_to_a_file_and_substitutes_its_path()
    {
        var lines = new List<string>();
        var agent = Create("/c type {promptFile}");

        await agent.RunAsync(
            new AgentRequest(_dir, "PROMPT-MARKER-12345"),
            (_, text) => { lines.Add(text); return Task.CompletedTask; },
            default);

        Assert.Contains(lines, l => l.Contains("PROMPT-MARKER-12345", StringComparison.Ordinal));
    }
}