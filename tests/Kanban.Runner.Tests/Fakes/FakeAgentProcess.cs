using Kanban.Core.Enums;
using Kanban.Runner.Agents;

namespace Kanban.Runner.Tests.Fakes;

public class FakeAgentProcess : IAgentProcess
{
    public int ExitCode { get; set; }
    public bool TimedOut { get; set; }
    public string[] Lines { get; set; } = { "starting", "done" };

    /// <summary>Written into .kanban/result.json in the working directory. Null writes no file.</summary>
    public string? ResultFileContent { get; set; } = """{"status":"completed","summary":"Did the thing."}""";

    public string? ReceivedPrompt { get; private set; }
    public string? ReceivedWorkingDirectory { get; private set; }

    public async Task<AgentExecution> RunAsync(
        AgentRequest request,
        Func<LogStream, string, Task> onLine,
        CancellationToken ct)
    {
        ReceivedPrompt = request.Prompt;
        ReceivedWorkingDirectory = request.WorkingDirectory;

        foreach (var line in Lines)
        {
            await onLine(LogStream.Stdout, line);
        }

        if (ResultFileContent is not null)
        {
            var dir = Path.Combine(request.WorkingDirectory, ".kanban");
            Directory.CreateDirectory(dir);
            await File.WriteAllTextAsync(Path.Combine(dir, "result.json"), ResultFileContent, ct);
        }

        return new AgentExecution(ExitCode, TimedOut);
    }
}